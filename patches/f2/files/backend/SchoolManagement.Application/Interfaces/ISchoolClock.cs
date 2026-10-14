namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// F2: the school's calendar date. "Today" means the date on the wall in the school's time zone (Settings),
/// not the server's UTC date, so 07:00 in India on the 15th is the 15th even though UTC still says the 14th.
/// Use it for business dates (attendance, "moved today"); keep UTC for timestamps (CreatedAt, audit, tokens).
/// </summary>
public interface ISchoolClock
{
    Task<DateOnly> TodayAsync(CancellationToken cancellationToken);
}
