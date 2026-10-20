namespace SchoolManagement.Application.DTOs.Timetable;

/// <summary>One lesson on a class's grid. DayOfWeek is Sunday = 0 ... Saturday = 6.</summary>
public record TimetableCellResponse(
    int EntryId,
    int DayOfWeek,
    int PeriodId,
    int SubjectId,
    string SubjectName,
    string SubjectCode,
    int? TeacherId,
    string? TeacherName,
    /// <summary>The room typed for this cell only (null = none).</summary>
    string? Room,
    /// <summary>What to show: the cell's own room, else the class section's room.</summary>
    string? EffectiveRoom);

/// <summary>A class's whole week: which days and periods to draw, and the lessons in them.</summary>
public record ClassTimetableResponse(
    int ClassSectionId,
    string ClassName,
    int AcademicYearId,
    string? ClassRoom,
    /// <summary>Columns to draw, Monday first: the school's working days plus any other day that holds a lesson.</summary>
    List<int> Days,
    List<TimetablePeriodResponse> Periods,
    List<TimetableCellResponse> Cells);

public record TimetableCellInput(int DayOfWeek, int PeriodId, int SubjectId, string? Room);

/// <summary>Sets (adds or replaces) the given cells. Cells not mentioned are left as they are.</summary>
public record SaveTimetableCellsRequest(List<TimetableCellInput> Cells);

public record SkippedTimetableCell(int DayOfWeek, string PeriodLabel, string SubjectName, string Reason);

/// <summary>What Copy From did: cells that were on the target and are gone, cells created, cells that could not be copied.</summary>
public record CopyTimetableResult(int Replaced, int Copied, List<SkippedTimetableCell> Skipped);

public record TeacherTimetableCell(
    int DayOfWeek,
    int PeriodId,
    int ClassSectionId,
    string ClassName,
    int SubjectId,
    string SubjectName,
    string? EffectiveRoom);

public record TeacherTimetableResponse(
    int TeacherId,
    string TeacherName,
    int AcademicYearId,
    List<int> Days,
    List<TimetablePeriodResponse> Periods,
    List<TeacherTimetableCell> Cells);
