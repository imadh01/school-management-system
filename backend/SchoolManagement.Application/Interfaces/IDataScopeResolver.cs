namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// Which records a read may return (Batch F1).
///
/// <see cref="Unrestricted"/>: the caller holds the module's full View, so everything.
/// <see cref="RelatedTo"/>: the caller holds only the module's ViewScoped, so only records related to
/// that user. "Related" is the union of three relationships, whatever the caller's role is called:
///   - teacher: class teacher or subject teacher of the student's CURRENT class, in the CURRENT academic year;
///   - parent:  a guardian link from a Parent row whose UserId is the caller;
///   - student: the Student row whose UserId is the caller.
/// A teacher who is also a parent at the school therefore sees both their classes and their own children.
///
/// The scope is resolved per request and never cached: assignment changes apply on the next request.
/// The filter itself is one reusable query (Infrastructure: DataScopeQueries).
/// </summary>
public sealed class DataScope
{
    private DataScope(int? userId) => UserId = userId;

    public static DataScope Unrestricted { get; } = new(null);

    public static DataScope RelatedTo(int userId) => new(userId);

    /// <summary>Null when unrestricted.</summary>
    public int? UserId { get; }

    public bool IsUnrestricted => UserId is null;

    public override string ToString() => IsUnrestricted ? "Unrestricted" : $"RelatedTo({UserId})";
}

/// <summary>How the caller reaches one student. Decides which fields a scoped caller may see.</summary>
[Flags]
public enum StudentAccess
{
    /// <summary>Not found, or out of scope. Both answer 404, with the same body.</summary>
    None = 0,
    /// <summary>The caller teaches the student's current class this year.</summary>
    Teaches = 1,
    /// <summary>The caller is linked to the student as a parent/guardian.</summary>
    Guardian = 2,
    /// <summary>The caller is the student.</summary>
    Self = 4,
    /// <summary>The caller holds the module's full View.</summary>
    Unrestricted = 8,
}

/// <summary>Turns the caller's permissions into a <see cref="DataScope"/> for one module.</summary>
public interface IDataScopeResolver
{
    /// <summary>
    /// <paramref name="fullPermission"/> held → Unrestricted; else <paramref name="scopedPermission"/>
    /// held → RelatedTo(caller); else ForbiddenException (the endpoint's policy normally stops that first).
    /// </summary>
    Task<DataScope> ResolveAsync(string fullPermission, string scopedPermission, CancellationToken cancellationToken);
}
