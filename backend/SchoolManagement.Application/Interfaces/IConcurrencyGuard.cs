using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// Optimistic concurrency: "I read this record when its version was X; save my changes only if it
/// is still X." If somebody else saved in between, SaveChanges fails and the API answers 409.
/// </summary>
public interface IConcurrencyGuard
{
    /// <param name="entity">A record that was loaded (tracked) by this request.</param>
    /// <param name="rowVersion">The version string the client received when it loaded the record.</param>
    void Expect(IHasRowVersion entity, string rowVersion);
}
