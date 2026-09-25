using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Interfaces;

public interface IAdmissionService
{
    Task<List<AdmissionResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<AdmissionResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<AdmissionResponse> CreateAsync(CreateAdmissionRequest request, CancellationToken cancellationToken);
    Task<AdmissionResponse> UpdateAsync(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken);
    Task<AdmissionResponse> ConfirmAdmissionAsync(int id, ConfirmAdmissionRequest request, CancellationToken cancellationToken);
    Task<AdmissionResponse> EnrollAsync(int id, EnrollAdmissionRequest request, CancellationToken cancellationToken);
    Task<AdmissionResponse> RejectAsync(int id, RejectAdmissionRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
