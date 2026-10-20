namespace SchoolManagement.Application.DTOs.AcademicYears;

/// <summary>A new year always starts as Upcoming and empty.</summary>
public record CreateAcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>Upcoming: name and both dates. Current or Ended: name and end date only. Closed: refused.</summary>
public record UpdateAcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate, string RowVersion);

/// <summary>
/// ExpectedCurrentYearId is the year the confirm dialog named as "the current year" (null = the dialog said there
/// was none). If the current year is different by the time the request arrives, nothing is changed.
/// </summary>
public record SetCurrentAcademicYearRequest(string RowVersion, int? ExpectedCurrentYearId);

/// <summary>ConfirmName must equal the year's name exactly: closing is one-way.</summary>
public record CloseAcademicYearRequest(string RowVersion, string ConfirmName);

public record CopyPeriodsResult(int Copied);
