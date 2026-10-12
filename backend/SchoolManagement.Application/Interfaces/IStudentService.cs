using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentService
{
    // Reads come in two shapes (F1). The controller picks one from the caller's DataScope:
    //   full    → Students.View holders, every student, every field;
    //   scoped  → Students.ViewScoped holders, related students only, reduced fields.
    // Out-of-scope ids answer 404 with the same body as ids that do not exist.

    /// <summary>Allergy text only when <paramref name="includeSensitive"/>; everyone gets HasAllergies.</summary>
    Task<List<StudentSummaryResponse>> GetAllAsync(bool includeSensitive, CancellationToken cancellationToken);
    Task<List<StudentScopedSummaryResponse>> GetAllScopedAsync(DataScope scope, CancellationToken cancellationToken);
    /// <summary>Full detail. Leaves a sensitive-read audit row (health + identity).</summary>
    Task<StudentResponse> GetByIdAsync(int id, bool includeSensitive, CancellationToken cancellationToken);
    /// <summary>Reduced detail for a related student. Leaves a sensitive-read audit row (allergy flag).</summary>
    Task<StudentScopedResponse> GetByIdScopedAsync(int id, DataScope scope, CancellationToken cancellationToken);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, bool includeSensitive, CancellationToken cancellationToken);
    Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, bool includeSensitive, CancellationToken cancellationToken);
    Task<StudentResponse> UpdateIdentityAsync(int id, UpdateStudentIdentityRequest request, CancellationToken cancellationToken);
    Task<List<StudentEnrollmentResponse>> GetEnrollmentsAsync(int studentId, DataScope scope, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
