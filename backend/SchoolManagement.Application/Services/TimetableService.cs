using SchoolManagement.Application.DTOs.Timetable;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class TimetableService : ITimetableService
{
    private readonly ITimetableRepository _timetable;
    private readonly IClassSectionRepository _classSections;
    private readonly IAcademicYearRepository _academicYears;
    private readonly ISubjectRepository _subjects;
    private readonly ITeacherRepository _teachers;
    private readonly ISchoolSettingsProvider _settings;
    private readonly IConcurrencyGuard _concurrency;

    public TimetableService(
        ITimetableRepository timetable,
        IClassSectionRepository classSections,
        IAcademicYearRepository academicYears,
        ISubjectRepository subjects,
        ITeacherRepository teachers,
        ISchoolSettingsProvider settings,
        IConcurrencyGuard concurrency)
    {
        _timetable = timetable;
        _classSections = classSections;
        _academicYears = academicYears;
        _subjects = subjects;
        _teachers = teachers;
        _settings = settings;
        _concurrency = concurrency;
    }

    // ================================================================== periods

    public async Task<List<TimetablePeriodResponse>> GetPeriodsAsync(int? academicYearId, CancellationToken cancellationToken)
    {
        var year = await ResolveYearAsync(academicYearId, cancellationToken);
        var periods = await _timetable.GetPeriodsAsync(year.Id, cancellationToken);
        var counts = await _timetable.CountCellsByPeriodAsync(year.Id, cancellationToken);
        return periods.Select(p => ToPeriodResponse(p, counts.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<TimetablePeriodResponse> CreatePeriodAsync(CreateTimetablePeriodRequest request, CancellationToken cancellationToken)
    {
        var year = await _academicYears.GetByIdAsync(request.AcademicYearId, cancellationToken)
            ?? throw new NotFoundException("Academic year could not be found.");
        AcademicYearRules.EnsureNotClosed(year);
        var (start, end) = ParseTimes(request.StartTime, request.EndTime);
        var label = request.Label.Trim();

        return await _timetable.ExecuteLockedAsync(year.Id, async () =>
        {
            var existing = await _timetable.GetPeriodsAsync(year.Id, cancellationToken);
            EnsurePeriodRules(existing, null, label, start, end, request.SortOrder);

            var period = new TimetablePeriod
            {
                AcademicYearId = year.Id,
                Label = label,
                StartTime = start,
                EndTime = end,
                Kind = request.Kind,
                SortOrder = request.SortOrder,
            };
            await _timetable.AddPeriodAsync(period, cancellationToken);
            return ToPeriodResponse(period, 0);
        }, cancellationToken);
    }

    public async Task<TimetablePeriodResponse> UpdatePeriodAsync(int id, UpdateTimetablePeriodRequest request, CancellationToken cancellationToken)
    {
        var period = await _timetable.GetPeriodAsync(id, cancellationToken)
            ?? throw new NotFoundException("Period could not be found.");
        _concurrency.Expect(period, request.RowVersion); // 409 if someone saved this period after it was loaded
        await EnsureYearOpenAsync(period.AcademicYearId, cancellationToken);

        var (start, end) = ParseTimes(request.StartTime, request.EndTime);
        var label = request.Label.Trim();

        return await _timetable.ExecuteLockedAsync(period.AcademicYearId, async () =>
        {
            var others = await _timetable.GetPeriodsAsync(period.AcademicYearId, cancellationToken);
            EnsurePeriodRules(others, id, label, start, end, request.SortOrder);

            var cellCount = await _timetable.CountCellsInPeriodAsync(id, cancellationToken);
            if (cellCount > 0 && request.Kind != PeriodKinds.Teaching)
                throw new BusinessRuleException(
                    $"{period.Label} has {cellCount} lesson(s) and cannot become a break. Clear them first.");

            period.Label = label;
            period.StartTime = start;
            period.EndTime = end;
            period.Kind = request.Kind;
            period.SortOrder = request.SortOrder;
            await _timetable.SaveChangesAsync(cancellationToken);
            return ToPeriodResponse(period, cellCount);
        }, cancellationToken);
    }

    public async Task DeletePeriodAsync(int id, CancellationToken cancellationToken)
    {
        var period = await _timetable.GetPeriodAsync(id, cancellationToken)
            ?? throw new NotFoundException("Period could not be found.");
        await EnsureYearOpenAsync(period.AcademicYearId, cancellationToken);

        await _timetable.ExecuteLockedAsync(period.AcademicYearId, async () =>
        {
            var cellCount = await _timetable.CountCellsInPeriodAsync(id, cancellationToken);
            if (cellCount > 0)
                throw new ConflictException(
                    $"{period.Label} is used by {cellCount} lesson(s) and cannot be deleted. Clear them first.");

            period.IsDeleted = true;
            period.DeletedAt = DateTime.UtcNow;
            await _timetable.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);
    }

    // ================================================================== class grid

    public async Task<ClassTimetableResponse> GetClassTimetableAsync(int classSectionId, CancellationToken cancellationToken)
    {
        var section = await GetClassOrThrowAsync(classSectionId, cancellationToken);
        return await BuildClassTimetableAsync(section, cancellationToken);
    }

    public async Task<ClassTimetableResponse> SaveCellsAsync(int classSectionId, SaveTimetableCellsRequest request, CancellationToken cancellationToken)
    {
        var section = await GetClassOrThrowAsync(classSectionId, cancellationToken);
        await EnsureYearOpenAsync(section.AcademicYearId, cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);
        var workingDays = WorkingDays.ToDays(settings.WorkingDays).ToHashSet();

        await _timetable.ExecuteLockedAsync(section.AcademicYearId, async () =>
        {
            var periods = (await _timetable.GetPeriodsAsync(section.AcademicYearId, cancellationToken))
                .ToDictionary(p => p.Id);
            var subjects = new Dictionary<int, Subject>();

            foreach (var input in request.Cells)
            {
                var day = (DayOfWeek)input.DayOfWeek;

                if (!periods.TryGetValue(input.PeriodId, out var period))
                    throw new BusinessRuleException("That period does not belong to this class's academic year.");
                if (period.Kind != PeriodKinds.Teaching)
                    throw new BusinessRuleException($"{period.Label} is a break; no lesson can be scheduled in it.");
                if (!workingDays.Contains(day))
                    throw new BusinessRuleException($"{day} is not a working day of the school.");

                if (!subjects.TryGetValue(input.SubjectId, out var subject))
                {
                    subject = await _subjects.GetByIdAsync(input.SubjectId, cancellationToken)
                        ?? throw new NotFoundException("Subject could not be found.");
                    subjects[input.SubjectId] = subject;
                }
                if (subject.ClassSectionId != section.Id)
                    throw new BusinessRuleException($"{subject.Name} is not a subject of {section.DisplayName}.");
                if (subject.Status != "Active")
                    throw new BusinessRuleException($"{subject.Name} is inactive and cannot be scheduled.");

                await EnsureTeacherFreeAsync(subject, day, period, section.Id, cancellationToken);

                var room = TimetableRules.CleanRoom(input.Room);
                var entry = await _timetable.GetEntryAsync(section.Id, day, period.Id, cancellationToken);
                if (entry is null)
                {
                    _timetable.AddEntry(new TimetableEntry
                    {
                        ClassSectionId = section.Id,
                        PeriodId = period.Id,
                        DayOfWeek = day,
                        SubjectId = subject.Id,
                        Room = room,
                    });
                }
                else
                {
                    entry.SubjectId = subject.Id;
                    entry.Room = room;
                }
            }

            await _timetable.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);

        return await BuildClassTimetableAsync(section, cancellationToken);
    }

    public async Task ClearCellAsync(int classSectionId, DayOfWeek day, int periodId, CancellationToken cancellationToken)
    {
        var section = await GetClassOrThrowAsync(classSectionId, cancellationToken);
        await EnsureYearOpenAsync(section.AcademicYearId, cancellationToken);

        await _timetable.ExecuteLockedAsync(section.AcademicYearId, async () =>
        {
            var entry = await _timetable.GetEntryAsync(section.Id, day, periodId, cancellationToken)
                ?? throw new NotFoundException("There is no lesson in that period.");
            entry.IsDeleted = true;
            entry.DeletedAt = DateTime.UtcNow;
            await _timetable.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);
    }

    public async Task<CopyTimetableResult> CopyFromAsync(int classSectionId, int sourceClassSectionId, CancellationToken cancellationToken)
    {
        if (classSectionId == sourceClassSectionId)
            throw new BusinessRuleException("Choose a different class to copy from.");

        var target = await GetClassOrThrowAsync(classSectionId, cancellationToken);
        var source = await GetClassOrThrowAsync(sourceClassSectionId, cancellationToken);
        if (target.AcademicYearId != source.AcademicYearId)
            throw new BusinessRuleException("Copy From works between classes of the same academic year.");
        await EnsureYearOpenAsync(target.AcademicYearId, cancellationToken);

        var settings = await _settings.GetAsync(cancellationToken);
        var workingDays = WorkingDays.ToDays(settings.WorkingDays).ToHashSet();

        return await _timetable.ExecuteLockedAsync(target.AcademicYearId, async () =>
        {
            var sourceEntries = await _timetable.GetClassEntriesAsync(source.Id, cancellationToken);
            var targetSubjects = (await _timetable.GetClassSubjectsAsync(target.Id, cancellationToken))
                .ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

            // Clear the target grid first and save, so the freed slots no longer count for the unique index.
            var existing = await _timetable.GetClassEntriesForUpdateAsync(target.Id, cancellationToken);
            foreach (var old in existing)
            {
                old.IsDeleted = true;
                old.DeletedAt = DateTime.UtcNow;
            }
            await _timetable.SaveChangesAsync(cancellationToken);

            var skipped = new List<SkippedTimetableCell>();
            var copied = 0;
            foreach (var src in sourceEntries)
            {
                var dayNumber = (int)src.DayOfWeek;
                var label = src.Period.Label;
                var subjectName = src.Subject.Name;

                if (!workingDays.Contains(src.DayOfWeek))
                {
                    skipped.Add(new SkippedTimetableCell(dayNumber, label, subjectName, $"{src.DayOfWeek} is not a working day."));
                    continue;
                }
                if (!targetSubjects.TryGetValue(src.Subject.Code, out var subject))
                {
                    skipped.Add(new SkippedTimetableCell(dayNumber, label, subjectName,
                        $"{target.DisplayName} has no active subject with code {src.Subject.Code}."));
                    continue;
                }
                if (subject.TeacherId is int teacherId)
                {
                    var clash = await _timetable.FindTeacherClashAsync(teacherId, src.DayOfWeek, src.PeriodId, target.Id, cancellationToken);
                    if (clash is not null)
                    {
                        skipped.Add(new SkippedTimetableCell(dayNumber, label, subjectName,
                            $"{subject.Teacher?.Name ?? "The teacher"} already teaches {clash.ClassName} at that time."));
                        continue;
                    }
                }

                _timetable.AddEntry(new TimetableEntry
                {
                    ClassSectionId = target.Id,
                    PeriodId = src.PeriodId,
                    DayOfWeek = src.DayOfWeek,
                    SubjectId = subject.Id,
                    Room = src.Room,
                });
                copied++;
            }

            await _timetable.SaveChangesAsync(cancellationToken);
            return new CopyTimetableResult(existing.Count, copied, skipped);
        }, cancellationToken);
    }

    // ================================================================== teacher's week

    public async Task<TeacherTimetableResponse> GetTeacherTimetableAsync(
        int teacherId, int? academicYearId, DataScope scope, CancellationToken cancellationToken)
    {
        var teacher = await _teachers.GetByIdAsync(teacherId, cancellationToken);

        // A scoped caller (Timetable.ViewScoped) may only see their own week. Another teacher's id answers 404, the
        // same as an id that does not exist, so the answer does not reveal who is a teacher.
        if (teacher is null || (!scope.IsUnrestricted && teacher.UserId != scope.UserId))
            throw new NotFoundException("Teacher could not be found.");

        var year = await ResolveYearAsync(academicYearId, cancellationToken);
        var periods = await _timetable.GetPeriodsAsync(year.Id, cancellationToken);
        var entries = await _timetable.GetTeacherEntriesAsync(teacherId, year.Id, cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);

        var cells = entries.Select(e => new TeacherTimetableCell(
            (int)e.DayOfWeek, e.PeriodId, e.ClassSectionId, e.ClassSection.DisplayName,
            e.SubjectId, e.Subject.Name, e.Room ?? e.ClassSection.Room)).ToList();

        return new TeacherTimetableResponse(
            teacher.Id, teacher.Name, year.Id,
            BuildDays(settings.WorkingDays, entries.Select(e => e.DayOfWeek)),
            periods.Select(p => ToPeriodResponse(p, 0)).ToList(),
            cells);
    }

    // ================================================================== shared with Teachers and Subjects

    public async Task EnsureTeacherCanTakeSubjectAsync(int subjectId, int? newTeacherId, CancellationToken cancellationToken)
    {
        if (newTeacherId is null) return; // unassigning never creates a clash

        var cells = await _timetable.GetSubjectEntriesAsync(subjectId, cancellationToken);
        if (cells.Count == 0) return;

        var teacher = await _teachers.GetByIdAsync(newTeacherId.Value, cancellationToken);
        var name = teacher?.Name ?? "This teacher";

        foreach (var cell in cells)
        {
            var clash = await _timetable.FindTeacherClashAsync(
                newTeacherId.Value, cell.DayOfWeek, cell.PeriodId, cell.ClassSectionId, cancellationToken);
            if (clash is not null)
                throw new ConflictException(
                    $"{name} already teaches {clash.ClassName} on {clash.Day} in {clash.PeriodLabel}, " +
                    $"the same time as {cell.ClassSection.DisplayName} has this subject.");
        }
    }

    public async Task EnsureSubjectNotOnTimetableAsync(int subjectId, string action, CancellationToken cancellationToken)
    {
        var cells = await _timetable.GetSubjectEntriesAsync(subjectId, cancellationToken);
        if (cells.Count > 0)
            throw new ConflictException(
                $"This subject is on the timetable ({cells.Count} lesson(s)) and cannot be {action}. Remove it from the timetable first.");
    }

    public async Task EnsureDaysNotUsedAsync(IReadOnlyCollection<DayOfWeek> days, CancellationToken cancellationToken)
    {
        if (days.Count == 0) return;

        var year = await _academicYears.GetCurrentAsync(cancellationToken);
        if (year is null) return; // no current year, so no timetable in use

        var (cells, classes) = await _timetable.CountCellsOnDaysAsync(year.Id, days, cancellationToken);
        if (cells == 0) return;

        var names = string.Join(", ", days.OrderBy(TimetableRules.DaySortKey).Select(d => d.ToString()));
        throw new ConflictException(
            $"Cannot remove {names} as a working day: {cells} lesson(s) in {classes} class(es) of {year.Name} " +
            "are scheduled on it. Clear those lessons from the timetable first.");
    }

    public Task<T> RunLockedAsync<T>(int academicYearId, Func<Task<T>> operation, CancellationToken cancellationToken) =>
        _timetable.ExecuteLockedAsync(academicYearId, operation, cancellationToken);

    // ================================================================== helpers

    private async Task<ClassSection> GetClassOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _classSections.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException("Class could not be found.");

    /// <summary>Every timetable change goes through this: a Closed year is read-only history (409).</summary>
    private async Task EnsureYearOpenAsync(int academicYearId, CancellationToken cancellationToken)
    {
        var year = await _academicYears.GetByIdAsync(academicYearId, cancellationToken)
            ?? throw new NotFoundException("Academic year could not be found.");
        AcademicYearRules.EnsureNotClosed(year);
    }

    private async Task<AcademicYear> ResolveYearAsync(int? academicYearId, CancellationToken cancellationToken)
    {
        if (academicYearId is int id)
            return await _academicYears.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Academic year could not be found.");

        return await _academicYears.GetCurrentAsync(cancellationToken)
            ?? throw new BusinessRuleException("There is no current academic year.");
    }

    /// <summary>The teacher of the subject must be free at this day and period in every other class (409 if not).</summary>
    private async Task EnsureTeacherFreeAsync(
        Subject subject, DayOfWeek day, TimetablePeriod period, int classSectionId, CancellationToken cancellationToken)
    {
        if (subject.TeacherId is not int teacherId) return; // no teacher assigned yet: nothing to clash with

        var clash = await _timetable.FindTeacherClashAsync(teacherId, day, period.Id, classSectionId, cancellationToken);
        if (clash is not null)
            throw new ConflictException(
                $"{subject.Teacher?.Name ?? "This teacher"} already teaches {clash.ClassName} on {clash.Day} in {clash.PeriodLabel}.");
    }

    private static (TimeOnly Start, TimeOnly End) ParseTimes(string startText, string endText)
    {
        // The validator has already checked the format; this only converts.
        TimetableRules.TryParseTime(startText, out var start);
        TimetableRules.TryParseTime(endText, out var end);
        return (start, end);
    }

    /// <summary>Label and order must be free, and the time range must not overlap another period of the year.</summary>
    private static void EnsurePeriodRules(
        IEnumerable<TimetablePeriod> periods, int? ignoreId, string label, TimeOnly start, TimeOnly end, int sortOrder)
    {
        foreach (var other in periods.Where(p => p.Id != (ignoreId ?? 0)))
        {
            if (string.Equals(other.Label, label, StringComparison.OrdinalIgnoreCase))
                throw new ConflictException($"A period called '{other.Label}' already exists in this academic year.");
            if (other.SortOrder == sortOrder)
                throw new ConflictException($"Order {sortOrder} is already used by '{other.Label}'.");
            if (TimetableRules.Overlap(start, end, other.StartTime, other.EndTime))
                throw new BusinessRuleException(
                    $"{TimetableRules.FormatTime(start)}-{TimetableRules.FormatTime(end)} overlaps '{other.Label}' " +
                    $"({TimetableRules.FormatTime(other.StartTime)}-{TimetableRules.FormatTime(other.EndTime)}).");
        }
    }

    private async Task<ClassTimetableResponse> BuildClassTimetableAsync(ClassSection section, CancellationToken cancellationToken)
    {
        var periods = await _timetable.GetPeriodsAsync(section.AcademicYearId, cancellationToken);
        var entries = await _timetable.GetClassEntriesAsync(section.Id, cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);

        var perPeriod = entries.GroupBy(e => e.PeriodId).ToDictionary(g => g.Key, g => g.Count());
        var cells = entries.Select(e => new TimetableCellResponse(
            e.Id, (int)e.DayOfWeek, e.PeriodId, e.SubjectId, e.Subject.Name, e.Subject.Code,
            e.Subject.TeacherId, e.Subject.Teacher?.Name, e.Room, e.Room ?? section.Room)).ToList();

        return new ClassTimetableResponse(
            section.Id, section.DisplayName, section.AcademicYearId, section.Room,
            BuildDays(settings.WorkingDays, entries.Select(e => e.DayOfWeek)),
            periods.Select(p => ToPeriodResponse(p, perPeriod.GetValueOrDefault(p.Id))).ToList(),
            cells);
    }

    /// <summary>Working days plus any other day that holds a lesson (so a lesson is never hidden), Monday first.</summary>
    private static List<int> BuildDays(byte workingDaysMask, IEnumerable<DayOfWeek> daysWithLessons) =>
        WorkingDays.ToDays(workingDaysMask)
            .Union(daysWithLessons)
            .OrderBy(TimetableRules.DaySortKey)
            .Select(d => (int)d)
            .ToList();

    private static TimetablePeriodResponse ToPeriodResponse(TimetablePeriod p, int cellCount) => new(
        p.Id, p.AcademicYearId, p.Label,
        TimetableRules.FormatTime(p.StartTime), TimetableRules.FormatTime(p.EndTime),
        p.Kind, p.SortOrder, cellCount, Convert.ToBase64String(p.RowVersion));
}
