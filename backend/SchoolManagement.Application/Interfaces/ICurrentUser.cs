namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// "Who is making this request?" - so the database layer can stamp CreatedBy / UpdatedBy and write the audit log
/// without every service passing the user around. Outside a web request (tests, seeding) every value is null.
/// </summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? UserName { get; }

    /// <summary>The request's correlation/trace ID.</summary>
    string? TraceId { get; }
}
