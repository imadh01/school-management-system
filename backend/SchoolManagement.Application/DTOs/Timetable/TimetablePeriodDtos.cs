namespace SchoolManagement.Application.DTOs.Timetable;

/// <summary>One slot of the school day. Times are school-local "HH:mm" text.</summary>
public record TimetablePeriodResponse(
    int Id,
    int AcademicYearId,
    string Label,
    string StartTime,
    string EndTime,
    string Kind,
    int SortOrder,
    /// <summary>How many live timetable cells use this period (a used period cannot be deleted).</summary>
    int CellCount,
    string RowVersion);

public record CreateTimetablePeriodRequest(
    int AcademicYearId,
    string Label,
    string StartTime,
    string EndTime,
    string Kind,
    int SortOrder);

public record UpdateTimetablePeriodRequest(
    string Label,
    string StartTime,
    string EndTime,
    string Kind,
    int SortOrder,
    string RowVersion);
