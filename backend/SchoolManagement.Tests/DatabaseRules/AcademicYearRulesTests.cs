using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.AcademicYears;
using SchoolManagement.Application.DTOs.ClassSections;
using SchoolManagement.Application.DTOs.Timetable;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// The Academic Years rules against a real SQL Server: create / edit rules, Set Current (including two at the same
/// moment), Close, Delete, Copy Periods, the closed-year guards and what switching the current year does to a
/// scoped teacher. Years made here use far-future dates (one calendar year each) so they never overlap each other
/// or the seeded year. Tests that switch the current year put the original one back in a finally block, because
/// other test classes read "the current year".
/// </summary>
[Collection("Database")]
public class AcademicYearRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public AcademicYearRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ helpers

    private static int _yearCounter = Random.Shared.Next(2200, 7000);

    /// <summary>Jan 1 to Dec 31 of a year nobody else uses (3 apart, so a test may also use the next year).</summary>
    private static (DateOnly Start, DateOnly End) NextRange()
    {
        var year = Interlocked.Add(ref _yearCounter, 3);
        return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
    }

    private static string NewName() => "AYT" + TestData.Unique();

    private async Task<T> SvcAsync<T>(Func<IAcademicYearService, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAcademicYearService>());
    }

    private async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>A year written straight to the database with the given (non-current) status.</summary>
    private Task<AcademicYear> NewYearAsync(string status = AcademicYearStatuses.Upcoming) => DbAsync(async db =>
    {
        var (start, end) = NextRange();
        var year = new AcademicYear { Name = NewName(), StartDate = start, EndDate = end, Status = status, IsCurrent = false };
        db.AcademicYears.Add(year);
        await db.SaveChangesAsync();
        return year;
    });

    private Task<string> RowVersionOfAsync(int id) => DbAsync(async db =>
        Convert.ToBase64String((await db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == id)).RowVersion));

    private Task<AcademicYear> LoadAsync(int id) => DbAsync(db =>
        db.AcademicYears.IgnoreQueryFilters().AsNoTracking().SingleAsync(y => y.Id == id));

    private Task<int> NewUserAsync() => DbAsync(async db =>
    {
        var name = "ay_" + TestData.Unique();
        var user = new User
        {
            Username = name, Email = $"{name}@test.local", PasswordHash = "not-a-real-hash",
            Status = UserStatuses.Active, SecurityStamp = Guid.NewGuid(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    });

    private Task<ClassSection> NewClassInAsync(int yearId, int? classTeacherId = null) => DbAsync(async db =>
    {
        var section = new ClassSection
        {
            AcademicYearId = yearId, Name = "Class " + TestData.Unique(), Section = "A",
            Status = "Active", ClassTeacherId = classTeacherId,
        };
        db.ClassSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    });

    /// <summary>Runs a test that changes the current year, then puts the original current year back.</summary>
    private async Task WithCurrentYearRestoredAsync(Func<int, Task> test)
    {
        var original = await DbAsync(db => db.AcademicYears.Where(y => y.IsCurrent).Select(y => y.Id).SingleAsync());
        try
        {
            await test(original);
        }
        finally
        {
            await DbAsync(async db =>
            {
                await db.AcademicYears.Where(y => y.Id != original && y.IsCurrent).ExecuteUpdateAsync(s => s
                    .SetProperty(y => y.IsCurrent, false)
                    .SetProperty(y => y.Status, AcademicYearStatuses.Ended));
                await db.AcademicYears.Where(y => y.Id == original).ExecuteUpdateAsync(s => s
                    .SetProperty(y => y.IsCurrent, true)
                    .SetProperty(y => y.Status, AcademicYearStatuses.Current));
                return 0;
            });
        }
    }

    private static async Task<Exception?> TryAsync(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception ex) { return ex; }
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task A_new_year_starts_Upcoming_not_current_and_empty()
    {
        var (start, end) = NextRange();
        var created = await SvcAsync(s => s.CreateAsync(new CreateAcademicYearRequest(NewName(), start, end), default));

        Assert.Equal(AcademicYearStatuses.Upcoming, created.Status);
        Assert.False(created.IsCurrent);
        Assert.Equal(0, created.ClassCount + created.ActiveEnrollmentCount + created.PeriodCount);
        Assert.False(string.IsNullOrEmpty(created.RowVersion));
    }

    [Fact]
    public async Task Dates_that_overlap_another_year_are_refused_but_the_next_day_is_fine()
    {
        var (start, end) = NextRange();
        await SvcAsync(s => s.CreateAsync(new CreateAcademicYearRequest(NewName(), start, end), default));

        // Starts inside the year.
        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.CreateAsync(
            new CreateAcademicYearRequest(NewName(), start.AddDays(100), end.AddDays(200)), default)));
        // Starts on the last day: both ends count as inside the year.
        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.CreateAsync(
            new CreateAcademicYearRequest(NewName(), end, end.AddDays(200)), default)));
        // Starts the day after: fine.
        var next = await SvcAsync(s => s.CreateAsync(
            new CreateAcademicYearRequest(NewName(), end.AddDays(1), end.AddDays(200)), default));
        Assert.Equal(AcademicYearStatuses.Upcoming, next.Status);
    }

    [Fact]
    public async Task A_name_cannot_be_used_twice_whatever_the_case()
    {
        var (start, end) = NextRange();
        var name = NewName();
        await SvcAsync(s => s.CreateAsync(new CreateAcademicYearRequest(name, start, end), default));

        var (start2, end2) = NextRange();
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.CreateAsync(
            new CreateAcademicYearRequest(name.ToLowerInvariant(), start2, end2), default)));
    }

    // ------------------------------------------------------------------ edit

    [Fact]
    public async Task An_Upcoming_year_can_change_name_and_dates_but_not_onto_another_year()
    {
        var year = await NewYearAsync();
        var other = await NewYearAsync();
        var version = await RowVersionOfAsync(year.Id);
        var newName = NewName();

        var updated = await SvcAsync(s => s.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(newName, year.StartDate.AddDays(10), year.EndDate.AddDays(-10), version), default));
        Assert.Equal(newName, updated.Name);
        Assert.Equal(year.StartDate.AddDays(10).ToString("yyyy-MM-dd"), updated.StartDate);

        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(newName, other.StartDate, other.EndDate, updated.RowVersion), default)));
    }

    [Fact]
    public async Task Editing_with_an_out_of_date_version_is_refused()
    {
        var year = await NewYearAsync();
        var stale = await RowVersionOfAsync(year.Id);

        await SvcAsync(s => s.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(NewName(), year.StartDate, year.EndDate, stale), default));

        // The stale version fails at SaveChanges; the middleware maps this exception to 409.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => SvcAsync(s => s.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(NewName(), year.StartDate, year.EndDate, stale), default)));
    }

    [Fact]
    public async Task The_dates_may_not_exclude_a_record_already_saved_in_the_year()
    {
        var year = await NewYearAsync();
        var section = await NewClassInAsync(year.Id);
        var userId = await NewUserAsync();
        var sessionDate = year.StartDate.AddDays(100);
        await DbAsync(async db =>
        {
            db.AttendanceSessions.Add(new AttendanceSession
            {
                ClassSectionId = section.Id, Date = sessionDate, TakenByUserId = userId,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var version = await RowVersionOfAsync(year.Id);
        // End before the attendance day.
        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(year.Name, year.StartDate, sessionDate.AddDays(-1), version), default)));
        // Start after the attendance day.
        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(year.Name, sessionDate.AddDays(1), year.EndDate, version), default)));
        // Still covering it: fine.
        var ok = await SvcAsync(s => s.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(year.Name, year.StartDate, sessionDate, version), default));
        Assert.Equal(sessionDate.ToString("yyyy-MM-dd"), ok.EndDate);
    }

    [Fact]
    public async Task An_Ended_year_keeps_its_start_date_and_a_Closed_year_cannot_be_edited()
    {
        var ended = await NewYearAsync(AcademicYearStatuses.Ended);
        var endedVersion = await RowVersionOfAsync(ended.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.UpdateAsync(ended.Id,
            new UpdateAcademicYearRequest(ended.Name, ended.StartDate.AddDays(1), ended.EndDate, endedVersion), default)));
        var ok = await SvcAsync(s => s.UpdateAsync(ended.Id,
            new UpdateAcademicYearRequest(ended.Name, ended.StartDate, ended.EndDate.AddDays(-5), endedVersion), default));
        Assert.Equal(ended.EndDate.AddDays(-5).ToString("yyyy-MM-dd"), ok.EndDate);

        var closed = await NewYearAsync(AcademicYearStatuses.Closed);
        var closedVersion = await RowVersionOfAsync(closed.Id);
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.UpdateAsync(closed.Id,
            new UpdateAcademicYearRequest(NewName(), closed.StartDate, closed.EndDate, closedVersion), default)));
    }

    // ------------------------------------------------------------------ set current

    [Fact]
    public async Task Setting_a_year_current_ends_the_old_one_and_leaves_exactly_one_current()
    {
        await WithCurrentYearRestoredAsync(async original =>
        {
            var year = await NewYearAsync();
            var version = await RowVersionOfAsync(year.Id);
            var updatesBefore = await DbAsync(db => db.AuditLogs.CountAsync(l => l.TableName == "AcademicYears"
                && l.RecordId == original.ToString() && l.Action == AuditActions.Update));

            var result = await SvcAsync(s => s.SetCurrentAsync(year.Id, new SetCurrentAcademicYearRequest(version, original), default));
            Assert.True(result.IsCurrent);
            Assert.Equal(AcademicYearStatuses.Current, result.Status);

            var old = await LoadAsync(original);
            Assert.Equal(AcademicYearStatuses.Ended, old.Status);
            Assert.False(old.IsCurrent);
            Assert.Equal(1, await DbAsync(db => db.AcademicYears.CountAsync(y => y.IsCurrent)));

            // Each year gets its own audit row for the change of status.
            await DbAsync(async db =>
            {
                Assert.Equal(updatesBefore + 1, await db.AuditLogs.CountAsync(l => l.TableName == "AcademicYears"
                    && l.RecordId == original.ToString() && l.Action == AuditActions.Update));
                Assert.True(await db.AuditLogs.AnyAsync(l => l.TableName == "AcademicYears"
                    && l.RecordId == year.Id.ToString() && l.Action == AuditActions.Update && l.Changes!.Contains("Current")));
                return 0;
            });
        });
    }

    [Fact]
    public async Task Only_an_Upcoming_year_can_be_made_current()
    {
        await WithCurrentYearRestoredAsync(async original =>
        {
            foreach (var status in new[] { AcademicYearStatuses.Ended, AcademicYearStatuses.Closed })
            {
                var year = await NewYearAsync(status);
                var version = await RowVersionOfAsync(year.Id);
                await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.SetCurrentAsync(
                    year.Id, new SetCurrentAcademicYearRequest(version, original), default)));
            }
            Assert.Equal(original, await DbAsync(db => db.AcademicYears.Where(y => y.IsCurrent).Select(y => y.Id).SingleAsync()));
        });
    }

    [Fact]
    public async Task A_confirm_that_named_a_different_current_year_changes_nothing()
    {
        await WithCurrentYearRestoredAsync(async original =>
        {
            var year = await NewYearAsync();
            var version = await RowVersionOfAsync(year.Id);

            // The dialog thought there was no current year (null) or a different one (original + 1000).
            await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.SetCurrentAsync(
                year.Id, new SetCurrentAcademicYearRequest(version, null), default)));
            await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.SetCurrentAsync(
                year.Id, new SetCurrentAcademicYearRequest(version, original + 1000), default)));

            Assert.Equal(AcademicYearStatuses.Upcoming, (await LoadAsync(year.Id)).Status);
            Assert.Equal(original, await DbAsync(db => db.AcademicYears.Where(y => y.IsCurrent).Select(y => y.Id).SingleAsync()));
        });
    }

    [Fact]
    public async Task Two_set_current_calls_at_the_same_moment_leave_exactly_one_current_year()
    {
        await WithCurrentYearRestoredAsync(async original =>
        {
            var a = await NewYearAsync();
            var b = await NewYearAsync();
            var versionA = await RowVersionOfAsync(a.Id);
            var versionB = await RowVersionOfAsync(b.Id);

            var results = await Task.WhenAll(
                TryAsync(() => SvcAsync(s => s.SetCurrentAsync(a.Id, new SetCurrentAcademicYearRequest(versionA, original), default))),
                TryAsync(() => SvcAsync(s => s.SetCurrentAsync(b.Id, new SetCurrentAcademicYearRequest(versionB, original), default))));

            // One wins; the other named a current year that is no longer current, so it is refused with a 409.
            Assert.Equal(1, results.Count(r => r is null));
            Assert.Single(results.Where(r => r is not null), r => r is ConflictException);
            Assert.Equal(1, await DbAsync(db => db.AcademicYears.CountAsync(y => y.IsCurrent)));
        });
    }

    [Fact]
    public async Task Switching_the_current_year_changes_what_a_scoped_teacher_sees_and_deletes_nothing()
    {
        await WithCurrentYearRestoredAsync(async original =>
        {
            var (teacherId, userId) = await NewTeacherAsync();
            var section = await NewClassInAsync(original, classTeacherId: teacherId);

            Assert.Contains(section.Id, await DbAsync(db => DataScopeQueries.TaughtSectionIds(db, userId).ToListAsync()));

            var next = await NewYearAsync();
            var version = await RowVersionOfAsync(next.Id);
            await SvcAsync(s => s.SetCurrentAsync(next.Id, new SetCurrentAcademicYearRequest(version, original), default));

            // The class is untouched, but the teacher's scope now looks at the new (empty) year.
            Assert.DoesNotContain(section.Id, await DbAsync(db => DataScopeQueries.TaughtSectionIds(db, userId).ToListAsync()));
            Assert.True(await DbAsync(db => db.ClassSections.AnyAsync(c => c.Id == section.Id)));
        });
    }

    private Task<(int TeacherId, int UserId)> NewTeacherAsync() => DbAsync(async db =>
    {
        var name = "ay_" + TestData.Unique();
        var user = new User
        {
            Username = name, Email = $"{name}@test.local", PasswordHash = "not-a-real-hash",
            Status = UserStatuses.Active, SecurityStamp = Guid.NewGuid(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var teacher = new Teacher { UserId = user.Id };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();
        return (teacher.Id, user.Id);
    });

    // ------------------------------------------------------------------ close

    [Fact]
    public async Task Only_an_Ended_year_can_be_closed_and_only_with_its_name_typed()
    {
        // Upcoming and Closed are refused by status.
        foreach (var status in new[] { AcademicYearStatuses.Upcoming, AcademicYearStatuses.Closed })
        {
            var y = await NewYearAsync(status);
            var v = await RowVersionOfAsync(y.Id);
            await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.CloseAsync(
                y.Id, new CloseAcademicYearRequest(v, y.Name), default)));
        }

        // The current year is refused: make another year current first.
        await WithCurrentYearRestoredAsync(async original =>
        {
            var current = await LoadAsync(original);
            await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.CloseAsync(
                original, new CloseAcademicYearRequest(Convert.ToBase64String(current.RowVersion), current.Name), default)));
        });

        var ended = await NewYearAsync(AcademicYearStatuses.Ended);
        var version = await RowVersionOfAsync(ended.Id);

        // A wrong (or differently cased) name changes nothing.
        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.CloseAsync(
            ended.Id, new CloseAcademicYearRequest(version, ended.Name.ToLowerInvariant()), default)));
        Assert.Equal(AcademicYearStatuses.Ended, (await LoadAsync(ended.Id)).Status);

        var closed = await SvcAsync(s => s.CloseAsync(ended.Id, new CloseAcademicYearRequest(version, ended.Name), default));
        Assert.Equal(AcademicYearStatuses.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAt);
        Assert.NotNull((await LoadAsync(ended.Id)).ClosedAt);
    }

    [Fact]
    public async Task A_year_with_an_Active_student_cannot_be_closed_until_the_student_has_left()
    {
        var year = await NewYearAsync(AcademicYearStatuses.Ended);
        var section = await NewClassInAsync(year.Id);
        var studentId = await DbAsync(async db =>
        {
            var student = TestData.NewStudent(section, "R" + TestData.Unique());
            db.Students.Add(student);
            await db.SaveChangesAsync();
            return student.Id;
        });
        var version = await RowVersionOfAsync(year.Id);

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.CloseAsync(
            year.Id, new CloseAcademicYearRequest(version, year.Name), default)));
        Assert.Contains("still Active", refused.Message);

        await DbAsync(async db =>
        {
            await db.StudentEnrollments.Where(e => e.StudentId == studentId).ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EnrollmentStatuses.Left)
                .SetProperty(e => e.EndDate, year.StartDate.AddDays(30)));
            return 0;
        });

        var closed = await SvcAsync(s => s.CloseAsync(year.Id, new CloseAcademicYearRequest(version, year.Name), default));
        Assert.Equal(AcademicYearStatuses.Closed, closed.Status);
    }

    [Fact]
    public async Task A_Closed_year_takes_no_new_classes_and_no_timetable_changes()
    {
        var year = await NewYearAsync(AcademicYearStatuses.Closed);

        await Assert.ThrowsAsync<ConflictException>(() => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<IClassSectionService>().CreateAsync(
                new CreateClassSectionRequest("Class " + TestData.Unique(), "A", null, "Primary", "English", "General",
                    30, null, null, null, year.Id), default));

        await Assert.ThrowsAsync<ConflictException>(() => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<ITimetableService>().CreatePeriodAsync(
                new CreateTimetablePeriodRequest(year.Id, "Period 1", "08:00", "08:45", PeriodKinds.Teaching, 1), default));
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task An_empty_Upcoming_year_is_soft_deleted_and_its_name_can_be_used_again()
    {
        var year = await NewYearAsync();

        await SvcAsync(async s => { await s.DeleteAsync(year.Id, default); return 0; });

        Assert.True((await LoadAsync(year.Id)).IsDeleted);
        await Assert.ThrowsAsync<NotFoundException>(() => SvcAsync(s => s.GetByIdAsync(year.Id, default)));

        var again = await SvcAsync(s => s.CreateAsync(new CreateAcademicYearRequest(year.Name, year.StartDate, year.EndDate), default));
        Assert.Equal(year.Name, again.Name);
    }

    [Fact]
    public async Task A_year_that_is_not_Upcoming_or_has_classes_or_periods_cannot_be_deleted()
    {
        var ended = await NewYearAsync(AcademicYearStatuses.Ended);
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(async s => { await s.DeleteAsync(ended.Id, default); return 0; }));

        var withClass = await NewYearAsync();
        await NewClassInAsync(withClass.Id);
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(async s => { await s.DeleteAsync(withClass.Id, default); return 0; }));

        var withPeriod = await NewYearAsync();
        await AddPeriodAsync(withPeriod.Id, "Period 1", "08:00", "08:45", PeriodKinds.Teaching, 1);
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(async s => { await s.DeleteAsync(withPeriod.Id, default); return 0; }));
    }

    // ------------------------------------------------------------------ copy periods

    private Task AddPeriodAsync(int yearId, string label, string start, string end, string kind, int order) => DbAsync(async db =>
    {
        db.TimetablePeriods.Add(new TimetablePeriod
        {
            AcademicYearId = yearId, Label = label, Kind = kind, SortOrder = order,
            StartTime = TimeOnly.Parse(start), EndTime = TimeOnly.Parse(end),
        });
        await db.SaveChangesAsync();
        return 0;
    });

    [Fact]
    public async Task Copy_periods_copies_exactly_the_source_periods_into_an_empty_year()
    {
        var source = await NewYearAsync(AcademicYearStatuses.Closed);
        await AddPeriodAsync(source.Id, "Period 1", "08:00", "08:45", PeriodKinds.Teaching, 1);
        await AddPeriodAsync(source.Id, "Snack", "08:45", "09:05", PeriodKinds.Break, 2);
        await AddPeriodAsync(source.Id, "Period 2", "09:05", "09:50", PeriodKinds.Teaching, 3);
        var target = await NewYearAsync();

        var result = await SvcAsync(s => s.CopyPeriodsAsync(target.Id, source.Id, default));
        Assert.Equal(3, result.Copied);

        var copied = await DbAsync(db => db.TimetablePeriods.AsNoTracking()
            .Where(p => p.AcademicYearId == target.Id).OrderBy(p => p.SortOrder).ToListAsync());
        Assert.Equal(new[] { "Period 1", "Snack", "Period 2" }, copied.Select(p => p.Label));
        Assert.Equal(new[] { PeriodKinds.Teaching, PeriodKinds.Break, PeriodKinds.Teaching }, copied.Select(p => p.Kind));
        Assert.Equal(new TimeOnly(9, 5), copied[2].StartTime);
        Assert.Equal(3, await DbAsync(db => db.TimetablePeriods.CountAsync(p => p.AcademicYearId == source.Id))); // source untouched
    }

    [Fact]
    public async Task Copy_periods_never_merges_and_refuses_a_bad_source_or_target()
    {
        var source = await NewYearAsync();
        await AddPeriodAsync(source.Id, "Period 1", "08:00", "08:45", PeriodKinds.Teaching, 1);
        var target = await NewYearAsync();
        await AddPeriodAsync(target.Id, "Mine", "09:00", "09:45", PeriodKinds.Teaching, 1);

        // Target already has a period: nothing is merged.
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.CopyPeriodsAsync(target.Id, source.Id, default)));
        Assert.Equal(1, await DbAsync(db => db.TimetablePeriods.CountAsync(p => p.AcademicYearId == target.Id)));

        // Same year, empty source, and a Closed or Ended target.
        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.CopyPeriodsAsync(source.Id, source.Id, default)));
        var emptySource = await NewYearAsync();
        var emptyTarget = await NewYearAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.CopyPeriodsAsync(emptyTarget.Id, emptySource.Id, default)));
        var closedTarget = await NewYearAsync(AcademicYearStatuses.Closed);
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.CopyPeriodsAsync(closedTarget.Id, source.Id, default)));
        var endedTarget = await NewYearAsync(AcademicYearStatuses.Ended);
        await Assert.ThrowsAsync<ConflictException>(() => SvcAsync(s => s.CopyPeriodsAsync(endedTarget.Id, source.Id, default)));
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task The_list_can_be_filtered_by_status_and_counts_what_belongs_to_each_year()
    {
        var year = await NewYearAsync();
        await NewClassInAsync(year.Id);
        await AddPeriodAsync(year.Id, "Period 1", "08:00", "08:45", PeriodKinds.Teaching, 1);

        var upcoming = await SvcAsync(s => s.GetAllAsync(AcademicYearStatuses.Upcoming, default));
        var row = Assert.Single(upcoming, y => y.Id == year.Id);
        Assert.Equal(1, row.ClassCount);
        Assert.Equal(1, row.PeriodCount);
        Assert.All(upcoming, y => Assert.Equal(AcademicYearStatuses.Upcoming, y.Status));

        await Assert.ThrowsAsync<BusinessRuleException>(() => SvcAsync(s => s.GetAllAsync("Bogus", default)));
    }
}
