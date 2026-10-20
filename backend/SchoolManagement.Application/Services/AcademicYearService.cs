using SchoolManagement.Application.DTOs.AcademicYears;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class AcademicYearService : IAcademicYearService
{
    private readonly IAcademicYearRepository _years;
    private readonly ITimetableRepository _timetable;
    private readonly IConcurrencyGuard _concurrency;
    private readonly ICurrentUser _currentUser;

    public AcademicYearService(
        IAcademicYearRepository years,
        ITimetableRepository timetable,
        IConcurrencyGuard concurrency,
        ICurrentUser currentUser)
    {
        _years = years;
        _timetable = timetable;
        _concurrency = concurrency;
        _currentUser = currentUser;
    }

    // ================================================================== reads

    public async Task<List<AcademicYearResponse>> GetAllAsync(string? status, CancellationToken cancellationToken)
    {
        if (status is not null && !AcademicYearStatuses.IsValid(status))
            throw new BusinessRuleException($"Unknown status '{status}'. Use Upcoming, Current, Ended or Closed.");

        var years = await _years.GetAllAsync(status, cancellationToken);
        var counts = await _years.GetCountsAsync(cancellationToken);
        return years.Select(y => ToResponse(y, counts.GetValueOrDefault(y.Id))).ToList();
    }

    public async Task<AcademicYearResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var year = await GetOrThrowAsync(id, cancellationToken);
        return await ToResponseAsync(year, cancellationToken);
    }

    // ================================================================== create / edit

    public async Task<AcademicYearResponse> CreateAsync(CreateAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        return await _years.ExecuteLockedAsync(async () =>
        {
            await EnsureNameFreeAsync(name, null, cancellationToken);
            await EnsureNoOverlapAsync(request.StartDate, request.EndDate, null, cancellationToken);

            var year = new AcademicYear
            {
                Name = name,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = AcademicYearStatuses.Upcoming,
                IsCurrent = false,
            };
            await _years.AddAsync(year, cancellationToken);
            return ToResponse(year, new AcademicYearCounts(0, 0, 0));
        }, cancellationToken);
    }

    public async Task<AcademicYearResponse> UpdateAsync(int id, UpdateAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var year = await GetOrThrowAsync(id, cancellationToken);
        _concurrency.Expect(year, request.RowVersion); // 409 if someone saved this year after it was loaded

        if (year.Status == AcademicYearStatuses.Closed)
            throw new ConflictException($"{year.Name} is closed and read-only.");

        var name = request.Name.Trim();

        return await _years.ExecuteLockedAsync(async () =>
        {
            // Dates are the base of every record in the year, so they are fixed once the year has started.
            if (year.Status != AcademicYearStatuses.Upcoming && request.StartDate != year.StartDate)
                throw new BusinessRuleException("The start date can only be changed while the year is Upcoming.");

            await EnsureNameFreeAsync(name, year.Id, cancellationToken);
            await EnsureNoOverlapAsync(request.StartDate, request.EndDate, year.Id, cancellationToken);

            // The dates may never exclude a record that already exists (an attendance day, an enrollment).
            var (earliest, latest) = await _years.GetRecordedDateRangeAsync(year.Id, cancellationToken);
            if ((earliest is not null && request.StartDate > earliest) || (latest is not null && request.EndDate < latest))
                throw new BusinessRuleException(
                    $"The dates must cover every record already saved in {year.Name} ({earliest:yyyy-MM-dd} to {latest:yyyy-MM-dd}).");

            year.Name = name;
            year.StartDate = request.StartDate;
            year.EndDate = request.EndDate;
            await _years.SaveChangesAsync(cancellationToken);
            return await ToResponseAsync(year, cancellationToken);
        }, cancellationToken);
    }

    // ================================================================== lifecycle

    public async Task<AcademicYearResponse> SetCurrentAsync(int id, SetCurrentAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var target = await GetOrThrowAsync(id, cancellationToken);
        _concurrency.Expect(target, request.RowVersion);

        return await _years.ExecuteLockedAsync(async () =>
        {
            if (target.Status != AcademicYearStatuses.Upcoming)
                throw new ConflictException($"Only an Upcoming year can be made current. {target.Name} is {target.Status}.");

            // The dialog named the current year to the user. If someone else switched it meanwhile, ask again.
            var current = await _years.GetCurrentAsync(cancellationToken);
            if (current?.Id != request.ExpectedCurrentYearId)
                throw new ConflictException("The current academic year changed while you were deciding. Reload and try again.");

            // Two saves, old year first: the filtered unique index allows one current row, and one UPDATE batch
            // could touch the new row before it clears the old one. Both saves are in the lock's transaction,
            // so they commit together; each writes its own audit row.
            if (current is not null)
            {
                current.Status = AcademicYearStatuses.Ended;
                current.IsCurrent = false;
                await _years.SaveChangesAsync(cancellationToken);
            }

            target.Status = AcademicYearStatuses.Current;
            target.IsCurrent = true;
            await _years.SaveChangesAsync(cancellationToken);
            return await ToResponseAsync(target, cancellationToken);
        }, cancellationToken);
    }

    public async Task<AcademicYearResponse> CloseAsync(int id, CloseAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var year = await GetOrThrowAsync(id, cancellationToken);
        _concurrency.Expect(year, request.RowVersion);

        if (year.Status != AcademicYearStatuses.Ended)
            throw new ConflictException(year.Status switch
            {
                AcademicYearStatuses.Current => "The current year cannot be closed. Make another year current first.",
                AcademicYearStatuses.Closed => $"{year.Name} is already closed.",
                _ => $"Only an Ended year can be closed. {year.Name} is {year.Status}.",
            });

        // Closing is one-way (no reopen), so the caller must type the name.
        if (!string.Equals(request.ConfirmName.Trim(), year.Name, StringComparison.Ordinal))
            throw new BusinessRuleException("Type the academic year name exactly to confirm closing.");

        return await _years.ExecuteLockedAsync(async () =>
        {
            var counts = (await _years.GetCountsAsync(cancellationToken)).GetValueOrDefault(year.Id);
            if (counts is { ActiveEnrollments: > 0 })
                throw new ConflictException(
                    $"{counts.ActiveEnrollments} student(s) are still Active in {year.Name}. " +
                    "Promote, repeat or transfer them first.");

            year.Status = AcademicYearStatuses.Closed;
            year.ClosedAt = DateTime.UtcNow;
            year.ClosedBy = _currentUser.UserId;
            await _years.SaveChangesAsync(cancellationToken);
            return await ToResponseAsync(year, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var year = await GetOrThrowAsync(id, cancellationToken);

        await _years.ExecuteLockedAsync(async () =>
        {
            // Upcoming means "never was current", so nothing can have been recorded against it by mistake of use.
            if (year.Status != AcademicYearStatuses.Upcoming)
                throw new ConflictException($"Only an Upcoming year can be deleted. {year.Name} is {year.Status}.");

            var counts = (await _years.GetCountsAsync(cancellationToken)).GetValueOrDefault(year.Id);
            if (counts is { Classes: > 0 } or { Periods: > 0 })
                throw new ConflictException(
                    $"{year.Name} has {counts!.Classes} class section(s) and {counts.Periods} period(s) and cannot be deleted.");

            year.IsDeleted = true;
            year.DeletedAt = DateTime.UtcNow;
            await _years.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);
    }

    // ================================================================== copy periods

    public async Task<CopyPeriodsResult> CopyPeriodsAsync(int id, int fromYearId, CancellationToken cancellationToken)
    {
        if (id == fromYearId)
            throw new BusinessRuleException("Choose a different year to copy the periods from.");

        var target = await GetOrThrowAsync(id, cancellationToken);
        var source = await GetOrThrowAsync(fromYearId, cancellationToken);

        if (target.Status is not (AcademicYearStatuses.Upcoming or AcademicYearStatuses.Current))
            throw new ConflictException($"Periods can only be copied into an Upcoming or Current year. {target.Name} is {target.Status}.");

        // The periods live under the Timetable lock of the target year, the same one Timetable uses to change them.
        return await _timetable.ExecuteLockedAsync(target.Id, async () =>
        {
            var existing = await _timetable.GetPeriodsAsync(target.Id, cancellationToken);
            if (existing.Count > 0)
                throw new ConflictException(
                    $"{target.Name} already has {existing.Count} period(s). Delete them first: copying never merges.");

            var periods = await _timetable.GetPeriodsAsync(source.Id, cancellationToken);
            if (periods.Count == 0)
                throw new BusinessRuleException($"{source.Name} has no periods to copy.");

            foreach (var p in periods)
            {
                await _timetable.AddPeriodAsync(new TimetablePeriod
                {
                    AcademicYearId = target.Id,
                    Label = p.Label,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    Kind = p.Kind,
                    SortOrder = p.SortOrder,
                }, cancellationToken);
            }

            return new CopyPeriodsResult(periods.Count);
        }, cancellationToken);
    }

    // ================================================================== helpers

    private async Task<AcademicYear> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _years.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException("Academic year could not be found.");

    private async Task EnsureNameFreeAsync(string name, int? ignoreId, CancellationToken cancellationToken)
    {
        if (await _years.NameExistsAsync(name, ignoreId, cancellationToken))
            throw new ConflictException($"An academic year named '{name}' already exists.");
    }

    private async Task EnsureNoOverlapAsync(DateOnly start, DateOnly end, int? ignoreId, CancellationToken cancellationToken)
    {
        var other = await _years.FindOverlappingAsync(start, end, ignoreId, cancellationToken);
        if (other is not null)
            throw new BusinessRuleException(
                $"These dates overlap {other.Name} ({other.StartDate:yyyy-MM-dd} to {other.EndDate:yyyy-MM-dd}).");
    }

    private async Task<AcademicYearResponse> ToResponseAsync(AcademicYear year, CancellationToken cancellationToken)
    {
        var counts = (await _years.GetCountsAsync(cancellationToken)).GetValueOrDefault(year.Id) ?? new AcademicYearCounts(0, 0, 0);
        return ToResponse(year, counts);
    }

    private static AcademicYearResponse ToResponse(AcademicYear y, AcademicYearCounts? counts)
    {
        counts ??= new AcademicYearCounts(0, 0, 0);
        return new AcademicYearResponse(
            y.Id, y.Name, y.StartDate.ToString("yyyy-MM-dd"), y.EndDate.ToString("yyyy-MM-dd"),
            y.IsCurrent, y.Status, Convert.ToBase64String(y.RowVersion),
            y.ClosedAt?.ToString("O"), counts.Classes, counts.ActiveEnrollments, counts.Periods);
    }
}
