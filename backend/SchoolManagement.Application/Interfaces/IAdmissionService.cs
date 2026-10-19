using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Interfaces;

public interface IAdmissionService
{
    Task<List<AdmissionResponse>> GetAllAsync(CancellationToken cancellationToken);
    /// <summary>Deleted applications with who deleted them and when (needs Admissions.Delete).</summary>
    Task<List<DeletedAdmissionResponse>> GetDeletedAsync(CancellationToken cancellationToken);
    Task<AdmissionResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    /// <param name="canLinkParents">G2: true only for callers holding Parents.View. Naming an existing parent without it is refused (403).</param>
    Task<AdmissionResponse> CreateAsync(CreateAdmissionRequest request, CancellationToken cancellationToken, bool canLinkParents = false);
    /// <param name="canLinkParents">G2: as for create. Keeping a parent link the application already has never needs it.</param>
    Task<AdmissionResponse> UpdateAsync(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken, bool canLinkParents = false);
    Task<AdmissionResponse> ConfirmAdmissionAsync(int id, ConfirmAdmissionRequest request, CancellationToken cancellationToken);
    /// <summary>G2: search live parents by name or mobile for the registration form. The caller must hold Parents.View.</summary>
    Task<List<ParentCandidateResponse>> SearchParentsAsync(string? query, int take, CancellationToken cancellationToken);
    Task<GuardianMatchesResponse> GetGuardianMatchesAsync(int id, CancellationToken cancellationToken);
    Task<AdmissionResponse> EnrollAsync(int id, EnrollAdmissionRequest request, CancellationToken cancellationToken);
    Task<AdmissionResponse> RejectAsync(int id, RejectAdmissionRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    /// <summary>Brings a deleted application back. Admin only; 404 unknown, 409 if not deleted or a unique value is taken.</summary>
    Task<AdmissionResponse> RestoreAsync(int id, CancellationToken cancellationToken);
}