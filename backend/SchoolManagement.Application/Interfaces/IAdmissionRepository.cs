using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>One existing Parent whose MobileKey equals a guardian's MobileKey.</summary>
public record ParentCandidateRow(int AdmissionGuardianId, int ParentId, string Name, string Mobile, string? Email);

/// <summary>G2: one parent found by the registration search.</summary>
public record ParentSummaryRow(int ParentId, string Name, string Mobile, string? Email);

/// <summary>A child already linked to a parent (shown so staff can tell families apart).</summary>
public record ParentChildRow(int ParentId, string StudentName);

public interface IAdmissionRepository
{
    Task<List<Admission>> GetAllAsync(CancellationToken cancellationToken);
    Task<Admission?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Admission admission, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

    /// <summary>
    /// Holds an exclusive lock on one application until the current transaction ends, so two
    /// enrolments of the SAME application run one after the other (the second then sees "Enrolled").
    /// Must be called inside <see cref="ExecuteInTransactionAsync{T}"/>.
    /// </summary>
    Task LockForEnrolmentAsync(int admissionId, CancellationToken cancellationToken);

    Task<List<ParentCandidateRow>> GetParentCandidatesAsync(int admissionId, CancellationToken cancellationToken);
    /// <summary>
    /// G2: live parents whose name contains <paramref name="nameText"/> or (when <paramref name="digits"/> is given)
    /// whose mobile contains those digits. Best matches first; at most <paramref name="take"/>.
    /// </summary>
    Task<List<ParentSummaryRow>> SearchParentsAsync(string? nameText, string? digits, int take, CancellationToken cancellationToken);

    Task<List<ParentChildRow>> GetLinkedChildrenAsync(IReadOnlyCollection<int> parentIds, CancellationToken cancellationToken);
}