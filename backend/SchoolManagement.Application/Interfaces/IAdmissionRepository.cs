using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>One existing Parent whose MobileKey equals a guardian's MobileKey.</summary>
public record ParentCandidateRow(int AdmissionGuardianId, int ParentId, string Name, string Mobile, string? Email);

/// <summary>A child already linked to a parent (shown so staff can tell families apart).</summary>
public record ParentChildRow(int ParentId, string StudentName);

public interface IAdmissionRepository
{
    Task<List<Admission>> GetAllAsync(CancellationToken cancellationToken);
    Task<Admission?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Admission admission, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

    Task<List<ParentCandidateRow>> GetParentCandidatesAsync(int admissionId, CancellationToken cancellationToken);
    Task<List<ParentChildRow>> GetLinkedChildrenAsync(IReadOnlyCollection<int> parentIds, CancellationToken cancellationToken);
}