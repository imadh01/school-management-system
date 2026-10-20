using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Settings;
using SchoolManagement.Application.DTOs.Subjects;
using SchoolManagement.Application.DTOs.Teachers;
using SchoolManagement.Application.DTOs.Timetable;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// The Timetable's rules against a real SQL Server: periods, one lesson per class/day/period, the teacher clash,
/// two saves at the same moment, Copy From, a teacher's own week, and the hooks in Teachers, Subjects and Settings.
/// Every test builds its own academic year, classes, subjects and teachers with unique values.
/// </summary>
[Collection("Database")]
public class TimetableRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public TimetableRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ setup helpers

    /// <summary>
    /// One academic year (not current) with two classes A and B, a Teaching period P1 and P2, a Break, and for each
    /// class a "Maths" subject taught by Teacher1 and an "English" subject taught by Teacher2.
    /// </summary>
    private sealed record World(
        int YearId, int ClassA, int ClassB,
        int P1, int P2, int BreakId,
        int MathsA, int MathsB, int EnglishA, int EnglishB,
        int Teacher1, int Teacher2, int Teacher1UserId, int Teacher2UserId);

    private async Task<World> NewWorldAsync()
    {
        await SetWorkingDaysAsync(WorkingDays.Monday | WorkingDays.Tuesday | WorkingDays.Wednesday
                                  | WorkingDays.Thursday | WorkingDays.Friday);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var a = await TestData.CreateSectionAsync(db);
        var b = await TestData.CreateSiblingSectionAsync(db, a, "B");

        var (t1, t1User) = await NewTeacherAsync(db);
        var (t2, t2User) = await NewTeacherAsync(db);

        Subject NewSubject(string name, string code, int classId, int teacherId) => new()
        {
            Name = name, Code = code, ClassSectionId = classId, TeacherId = teacherId,
            Type = "Theory", MaxMarks = 100, PassMarks = 35, Status = "Active",
        };
        var mathsA = NewSubject("Maths", "MATH", a.Id, t1);
        var mathsB = NewSubject("Maths", "MATH", b.Id, t1);
        var englishA = NewSubject("English", "ENG", a.Id, t2);
        var englishB = NewSubject("English", "ENG", b.Id, t2);
        db.Subjects.AddRange(mathsA, mathsB, englishA, englishB);

        TimetablePeriod Period(string label, string start, string end, string kind, int order) => new()
        {
            AcademicYearId = a.AcademicYearId, Label = label, Kind = kind, SortOrder = order,
            StartTime = TimeOnly.Parse(start), EndTime = TimeOnly.Parse(end),
        };
        var p1 = Period("Period 1", "08:00", "08:45", PeriodKinds.Teaching, 1);
        var p2 = Period("Period 2", "08:45", "09:30", PeriodKinds.Teaching, 2);
        var brk = Period("Snack", "09:30", "09:50", PeriodKinds.Break, 3);
        db.TimetablePeriods.AddRange(p1, p2, brk);
        await db.SaveChangesAsync();

        return new World(a.AcademicYearId, a.Id, b.Id, p1.Id, p2.Id, brk.Id,
            mathsA.Id, mathsB.Id, englishA.Id, englishB.Id, t1, t2, t1User, t2User);
    }

    private static async Task<(int TeacherId, int UserId)> NewTeacherAsync(ApplicationDbContext db)
    {
        var name = "tt_" + TestData.Unique();
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
    }

    /// <summary>Other test classes save the Settings page, so each test sets the days it needs.</summary>
    private async Task SetWorkingDaysAsync(byte mask)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.SchoolSettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.WorkingDays, mask));
        scope.ServiceProvider.GetRequiredService<ISchoolSettingsProvider>().Invalidate();
    }

    private static TimetableCellInput Cell(DayOfWeek day, int periodId, int subjectId, string? room = null) =>
        new((int)day, periodId, subjectId, room);

    private Task SaveAsync(int classId, params TimetableCellInput[] cells) =>
        InScopeAsync(s => s.SaveCellsAsync(classId, new SaveTimetableCellsRequest(cells.ToList()), default));

    private async Task InScopeAsync(Func<ITimetableService, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ITimetableService>());
    }

    private async Task<T> InScopeAsync<T>(Func<ITimetableService, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ITimetableService>());
    }

    private async Task<int> LiveCellsAsync(int classId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.TimetableEntries.CountAsync(e => e.ClassSectionId == classId);
    }

    // ------------------------------------------------------------------ periods

    [Fact]
    public async Task A_period_that_overlaps_another_is_refused_but_one_that_only_touches_is_fine()
    {
        var w = await NewWorldAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => InScopeAsync(s => s.CreatePeriodAsync(
            new CreateTimetablePeriodRequest(w.YearId, "Overlap", "08:30", "09:00", PeriodKinds.Teaching, 10), default)));

        var ok = await InScopeAsync(s => s.CreatePeriodAsync(
            new CreateTimetablePeriodRequest(w.YearId, "After snack", "09:50", "10:35", PeriodKinds.Teaching, 4), default));
        Assert.Equal("09:50", ok.StartTime);
    }

    [Fact]
    public async Task A_period_cannot_reuse_a_label_or_an_order_of_the_same_year()
    {
        var w = await NewWorldAsync();

        await Assert.ThrowsAsync<ConflictException>(() => InScopeAsync(s => s.CreatePeriodAsync(
            new CreateTimetablePeriodRequest(w.YearId, "period 1", "10:00", "10:45", PeriodKinds.Teaching, 9), default)));
        await Assert.ThrowsAsync<ConflictException>(() => InScopeAsync(s => s.CreatePeriodAsync(
            new CreateTimetablePeriodRequest(w.YearId, "Fresh label", "10:00", "10:45", PeriodKinds.Teaching, 1), default)));
    }

    [Fact]
    public async Task A_period_with_lessons_cannot_be_deleted_until_they_are_cleared()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA));

        await Assert.ThrowsAsync<ConflictException>(() => InScopeAsync(s => s.DeletePeriodAsync(w.P1, default)));

        await InScopeAsync(s => s.ClearCellAsync(w.ClassA, DayOfWeek.Monday, w.P1, default));
        await InScopeAsync(s => s.DeletePeriodAsync(w.P1, default));

        var periods = await InScopeAsync(s => s.GetPeriodsAsync(w.YearId, default));
        Assert.DoesNotContain(periods, p => p.Id == w.P1);
    }

    [Fact]
    public async Task A_period_with_lessons_cannot_become_a_break()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA));
        var p1 = (await InScopeAsync(s => s.GetPeriodsAsync(w.YearId, default))).Single(p => p.Id == w.P1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => InScopeAsync(s => s.UpdatePeriodAsync(w.P1,
            new UpdateTimetablePeriodRequest(p1.Label, p1.StartTime, p1.EndTime, PeriodKinds.Break, p1.SortOrder, p1.RowVersion),
            default)));
    }

    [Fact]
    public async Task Editing_a_period_with_an_out_of_date_version_is_refused()
    {
        var w = await NewWorldAsync();
        var p2 = (await InScopeAsync(s => s.GetPeriodsAsync(w.YearId, default))).Single(p => p.Id == w.P2);

        await InScopeAsync(s => s.UpdatePeriodAsync(w.P2,
            new UpdateTimetablePeriodRequest("Period Two", p2.StartTime, p2.EndTime, p2.Kind, p2.SortOrder, p2.RowVersion), default));

        // Like every other edit: the stale version fails the save, and the API's error middleware turns it into a 409.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => InScopeAsync(s => s.UpdatePeriodAsync(w.P2,
            new UpdateTimetablePeriodRequest("Period Too", p2.StartTime, p2.EndTime, p2.Kind, p2.SortOrder, p2.RowVersion), default)));
    }

    // ------------------------------------------------------------------ cells

    [Fact]
    public async Task A_lesson_cannot_go_into_a_break_a_closed_day_or_another_classes_subject()
    {
        var w = await NewWorldAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.BreakId, w.MathsA)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => SaveAsync(w.ClassA, Cell(DayOfWeek.Saturday, w.P1, w.MathsA)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsB)));

        Assert.Equal(0, await LiveCellsAsync(w.ClassA));
    }

    [Fact]
    public async Task An_inactive_subject_cannot_be_scheduled()
    {
        var w = await NewWorldAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Subjects.Where(s => s.Id == w.EnglishA).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Inactive"));
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.EnglishA)));
    }

    [Fact]
    public async Task A_period_of_another_academic_year_is_refused()
    {
        var w = await NewWorldAsync();
        var other = await NewWorldAsync(); // a different year with its own periods

        await Assert.ThrowsAsync<BusinessRuleException>(() => SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, other.P1, w.MathsA)));
    }

    [Fact]
    public async Task Saving_the_same_slot_again_replaces_the_lesson_instead_of_adding_a_second()
    {
        var w = await NewWorldAsync();

        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA, "R1"));
        var grid = await InScopeAsync(s => s.SaveCellsAsync(w.ClassA,
            new SaveTimetableCellsRequest(new() { Cell(DayOfWeek.Monday, w.P1, w.EnglishA, "R2") }), default));

        var cell = Assert.Single(grid.Cells);
        Assert.Equal(w.EnglishA, cell.SubjectId);
        Assert.Equal("R2", cell.Room);
        Assert.Equal(1, await LiveCellsAsync(w.ClassA));
    }

    [Fact]
    public async Task A_cell_without_its_own_room_shows_the_class_room()
    {
        var w = await NewWorldAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.ClassSections.Where(c => c.Id == w.ClassA).ExecuteUpdateAsync(c => c.SetProperty(x => x.Room, "A-101"));
        }

        var grid = await InScopeAsync(s => s.SaveCellsAsync(w.ClassA,
            new SaveTimetableCellsRequest(new() { Cell(DayOfWeek.Monday, w.P1, w.MathsA, "  ") }), default));

        var cell = Assert.Single(grid.Cells);
        Assert.Null(cell.Room);
        Assert.Equal("A-101", cell.EffectiveRoom);
    }

    [Fact]
    public async Task The_grid_shows_a_day_that_has_a_lesson_even_when_it_is_no_longer_a_working_day()
    {
        var w = await NewWorldAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TimetableEntries.Add(new TimetableEntry
            {
                ClassSectionId = w.ClassA, PeriodId = w.P1, SubjectId = w.MathsA, DayOfWeek = DayOfWeek.Saturday,
            });
            await db.SaveChangesAsync();
        }

        var grid = await InScopeAsync(s => s.GetClassTimetableAsync(w.ClassA, default));

        Assert.Equal(new List<int> { 1, 2, 3, 4, 5, 6 }, grid.Days); // Monday first, Saturday last
    }

    // ------------------------------------------------------------------ teacher clash

    [Fact]
    public async Task A_teacher_cannot_be_in_two_classes_at_the_same_day_and_period()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            SaveAsync(w.ClassB, Cell(DayOfWeek.Monday, w.P1, w.MathsB)));

        Assert.Contains("Class 1", ex.Message);
        Assert.Contains("Monday", ex.Message);
        Assert.Equal(0, await LiveCellsAsync(w.ClassB));

        // Another period, another day, or another teacher's subject are all fine.
        await SaveAsync(w.ClassB, Cell(DayOfWeek.Monday, w.P2, w.MathsB));
        await SaveAsync(w.ClassB, Cell(DayOfWeek.Tuesday, w.P1, w.MathsB));
        await SaveAsync(w.ClassB, Cell(DayOfWeek.Monday, w.P1, w.EnglishB));
    }

    [Fact]
    public async Task One_request_with_a_clash_saves_nothing()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA));

        await Assert.ThrowsAsync<ConflictException>(() => SaveAsync(w.ClassB,
            Cell(DayOfWeek.Tuesday, w.P1, w.EnglishB),   // fine on its own
            Cell(DayOfWeek.Monday, w.P1, w.MathsB)));    // clashes

        Assert.Equal(0, await LiveCellsAsync(w.ClassB));
    }

    [Fact]
    public async Task Two_saves_at_the_same_moment_that_would_double_book_a_teacher_let_exactly_one_through()
    {
        var w = await NewWorldAsync();

        var results = await Task.WhenAll(
            TryAsync(() => SaveAsync(w.ClassA, Cell(DayOfWeek.Wednesday, w.P1, w.MathsA))),
            TryAsync(() => SaveAsync(w.ClassB, Cell(DayOfWeek.Wednesday, w.P1, w.MathsB))));

        Assert.Equal(1, results.Count(r => r is null));
        Assert.Single(results.Where(r => r is not null), r => r is ConflictException);
        Assert.Equal(1, await LiveCellsAsync(w.ClassA) + await LiveCellsAsync(w.ClassB));
    }

    private static async Task<Exception?> TryAsync(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception ex) { return ex; }
    }

    // ------------------------------------------------------------------ hooks in Teachers and Subjects

    [Fact]
    public async Task Giving_a_subject_to_a_teacher_who_is_busy_at_its_lessons_is_refused_and_changes_nothing()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA));      // Teacher1
        await SaveAsync(w.ClassB, Cell(DayOfWeek.Monday, w.P1, w.EnglishB));    // Teacher2, same slot

        using var scope = _factory.Services.CreateScope();
        var teachers = scope.ServiceProvider.GetRequiredService<ITeacherService>();

        // Free English in class B from Teacher2, then try to give it to Teacher1 (busy in class A then).
        await teachers.UnassignSubjectAsync(w.Teacher2, w.EnglishB, default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            teachers.AssignSubjectsAsync(w.Teacher1, new AssignSubjectsRequest(w.ClassB, new() { w.EnglishB }), default));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await db.Subjects.AsNoTracking().SingleAsync(s => s.Id == w.EnglishB)).TeacherId);

        // The other teacher is free then, so the assignment goes through.
        await teachers.AssignSubjectsAsync(w.Teacher2, new AssignSubjectsRequest(w.ClassB, new() { w.EnglishB }), default);
    }

    [Fact]
    public async Task Taking_a_teacher_off_a_subject_that_is_on_the_timetable_is_always_allowed()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA));

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ITeacherService>().UnassignSubjectAsync(w.Teacher1, w.MathsA, default);

        var grid = await InScopeAsync(s => s.GetClassTimetableAsync(w.ClassA, default));
        var cell = Assert.Single(grid.Cells);
        Assert.Null(cell.TeacherName); // the lesson stays, with no teacher
    }

    [Fact]
    public async Task A_subject_on_the_timetable_cannot_move_to_another_class_or_be_deleted_until_cleared()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.EnglishA));

        using var scope = _factory.Services.CreateScope();
        var subjects = scope.ServiceProvider.GetRequiredService<ISubjectService>();
        var move = new UpdateSubjectRequest("English", "ENG2", w.ClassB, "Theory", 100, 35, null, null, null, null, true);

        await Assert.ThrowsAsync<ConflictException>(() => subjects.UpdateAsync(w.EnglishA, move, default));
        await Assert.ThrowsAsync<ConflictException>(() => subjects.DeleteAsync(w.EnglishA, default));

        // Edits that do not move it are fine.
        var rename = new UpdateSubjectRequest("English Lang", "ENG", w.ClassA, "Theory", 100, 35, null, null, null, null, true);
        await subjects.UpdateAsync(w.EnglishA, rename, default);

        await InScopeAsync(s => s.ClearCellAsync(w.ClassA, DayOfWeek.Monday, w.P1, default));
        await subjects.UpdateAsync(w.EnglishA, move, default);
        await subjects.DeleteAsync(w.EnglishA, default);
    }

    // ------------------------------------------------------------------ Copy From

    [Fact]
    public async Task Copy_From_matches_subjects_by_code_replaces_the_target_and_reports_what_it_skipped()
    {
        var w = await NewWorldAsync();
        // Class A: Maths Mon P1, English Mon P2, English Tue P1.
        await SaveAsync(w.ClassA,
            Cell(DayOfWeek.Monday, w.P1, w.MathsA, "R1"),
            Cell(DayOfWeek.Monday, w.P2, w.EnglishA),
            Cell(DayOfWeek.Tuesday, w.P1, w.EnglishA));
        // Class B already has a lesson, which Copy From must replace.
        await SaveAsync(w.ClassB, Cell(DayOfWeek.Friday, w.P2, w.MathsB));

        // The same teacher teaches a code in both classes, so copying that lesson to the same day and period would put
        // the teacher in two places and is skipped. Give B's English to a different teacher so those two lessons copy.
        int freshTeacher;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            freshTeacher = (await NewTeacherAsync(db)).TeacherId;
            await db.Subjects.Where(s => s.Id == w.EnglishB).ExecuteUpdateAsync(s => s.SetProperty(x => x.TeacherId, freshTeacher));
        }

        var result = await InScopeAsync(s => s.CopyFromAsync(w.ClassB, w.ClassA, default));

        Assert.Equal(1, result.Replaced);   // the Friday Maths of class B
        Assert.Equal(2, result.Copied);     // the two English lessons, now with the new teacher
        var skipped = Assert.Single(result.Skipped); // Maths at Monday P1: Teacher1 is teaching class A then
        Assert.Equal("Maths", skipped.SubjectName);
        Assert.Contains("already teaches", skipped.Reason);

        var grid = await InScopeAsync(s => s.GetClassTimetableAsync(w.ClassB, default));
        Assert.Equal(2, grid.Cells.Count);
        Assert.All(grid.Cells, c => Assert.Equal(w.EnglishB, c.SubjectId));
        Assert.DoesNotContain(grid.Cells, c => c.DayOfWeek == (int)DayOfWeek.Friday); // the old lesson is gone
    }

    [Fact]
    public async Task Copy_From_skips_a_subject_code_the_target_does_not_have()
    {
        var w = await NewWorldAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Subjects.Where(s => s.Id == w.EnglishB).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Inactive"));
        }
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.EnglishA));

        var result = await InScopeAsync(s => s.CopyFromAsync(w.ClassB, w.ClassA, default));

        Assert.Equal(0, result.Copied);
        Assert.Contains("no active subject with code ENG", Assert.Single(result.Skipped).Reason);
    }

    [Fact]
    public async Task Copy_From_needs_two_different_classes_of_the_same_year()
    {
        var w = await NewWorldAsync();
        var other = await NewWorldAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => InScopeAsync(s => s.CopyFromAsync(w.ClassA, w.ClassA, default)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => InScopeAsync(s => s.CopyFromAsync(w.ClassA, other.ClassA, default)));
    }

    // ------------------------------------------------------------------ a teacher's own week

    [Fact]
    public async Task A_scoped_teacher_sees_their_own_week_and_gets_404_for_anyone_elses()
    {
        var w = await NewWorldAsync();
        await SaveAsync(w.ClassA, Cell(DayOfWeek.Monday, w.P1, w.MathsA));

        var own = await InScopeAsync(s => s.GetTeacherTimetableAsync(
            w.Teacher1, w.YearId, DataScope.RelatedTo(w.Teacher1UserId), default));
        Assert.Single(own.Cells);

        await Assert.ThrowsAsync<NotFoundException>(() => InScopeAsync(s => s.GetTeacherTimetableAsync(
            w.Teacher1, w.YearId, DataScope.RelatedTo(w.Teacher2UserId), default)));
        await Assert.ThrowsAsync<NotFoundException>(() => InScopeAsync(s => s.GetTeacherTimetableAsync(
            999_999, w.YearId, DataScope.RelatedTo(w.Teacher1UserId), default))); // same answer as "not yours"

        var all = await InScopeAsync(s => s.GetTeacherTimetableAsync(w.Teacher1, w.YearId, DataScope.Unrestricted, default));
        Assert.Single(all.Cells);
    }

    // ------------------------------------------------------------------ Settings: closing a working day

    [Fact]
    public async Task A_working_day_with_lessons_in_the_current_year_cannot_be_removed_in_Settings()
    {
        await SetWorkingDaysAsync(WorkingDays.Monday | WorkingDays.Tuesday | WorkingDays.Wednesday
                                  | WorkingDays.Thursday | WorkingDays.Friday);

        // A class, a period and a Wednesday lesson in the CURRENT academic year.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var year = await db.AcademicYears.SingleAsync(y => y.IsCurrent);
            var section = new ClassSection
            {
                AcademicYearId = year.Id, Name = "TT Settings", Section = TestData.Unique()[..5], Status = "Active",
            };
            db.ClassSections.Add(section);
            await db.SaveChangesAsync();
            var subject = new Subject
            {
                Name = "Art", Code = "ART", ClassSectionId = section.Id, Type = "Theory", MaxMarks = 100, PassMarks = 35,
            };
            var period = new TimetablePeriod
            {
                AcademicYearId = year.Id, Label = "TT " + TestData.Unique(), Kind = PeriodKinds.Teaching,
                SortOrder = 20 + Random.Shared.Next(1, 9), StartTime = new TimeOnly(15, 0), EndTime = new TimeOnly(15, 30),
            };
            db.Subjects.Add(subject);
            db.TimetablePeriods.Add(period);
            await db.SaveChangesAsync();
            db.TimetableEntries.Add(new TimetableEntry
            {
                ClassSectionId = section.Id, PeriodId = period.Id, SubjectId = subject.Id, DayOfWeek = DayOfWeek.Wednesday,
            });
            await db.SaveChangesAsync();
        }

        using var scope2 = _factory.Services.CreateScope();
        var settings = scope2.ServiceProvider.GetRequiredService<ISchoolSettingsService>();
        var current = await settings.GetAsync(default);
        UpdateSchoolSettingsRequest Request(params string[] days) => new(
            current.SchoolName, current.ShortName, current.Email, current.Phone, current.Website, current.AddressText,
            current.CountryId, current.TimeZoneId, current.CurrencyCode, days.ToList(), current.AboutTagline, current.RowVersion);

        // Removing Wednesday is refused, and the saved days stay as they were.
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            settings.UpdateAsync(Request("Monday", "Tuesday", "Thursday", "Friday"), default));
        Assert.Contains("Wednesday", ex.Message);
        Assert.Contains("lesson", ex.Message);
        Assert.Equal(5, (await settings.GetAsync(default)).WorkingDays.Count);

        // A day with no lessons can be removed (nothing is scheduled on Friday or any other day).
        await settings.UpdateAsync(Request("Monday", "Tuesday", "Wednesday", "Thursday"), default);
        Assert.Equal(4, (await settings.GetAsync(default)).WorkingDays.Count);

        // Adding days is never refused.
        var after = await settings.GetAsync(default);
        await settings.UpdateAsync(Request("Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday") with
        {
            RowVersion = after.RowVersion,
        }, default);

        // Leave the school on Monday to Friday for the tests that follow.
        await SetWorkingDaysAsync(WorkingDays.Monday | WorkingDays.Tuesday | WorkingDays.Wednesday
                                  | WorkingDays.Thursday | WorkingDays.Friday);
    }
}
