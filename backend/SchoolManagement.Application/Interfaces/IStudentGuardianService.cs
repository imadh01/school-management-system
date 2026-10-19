using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentGuardianService
{
    /// <summary>Every guardian of the student, full contact (Students.View). 404 for an unknown student.</summary>
    Task<List<StudentGuardianResponse>> GetForStudentAsync(int studentId, CancellationToken cancellationToken);
    /// <summary>
    /// Guardians as a scoped caller sees them (F1): name, mobile, relation. A teacher (or the student)
    /// sees every guardian; a parent sees only their own link. 404 when the student is out of scope.
    /// </summary>
    Task<List<GuardianContactResponse>> GetContactsForStudentAsync(int studentId, DataScope scope, CancellationToken cancellationToken);
    Task<StudentGuardianResponse> LinkAsync(int studentId, LinkGuardianRequest request, CancellationToken cancellationToken);
    Task UnlinkAsync(int studentId, int parentId, CancellationToken cancellationToken);

    /// <summary>
    /// The parent's children (F1): 404 when the parent is missing or outside <paramref name="scope"/>;
    /// children outside the scope are left out (a teacher sees only the children they teach).
    /// </summary>
    Task<List<LinkedStudentResponse>> GetForParentAsync(int parentId, DataScope scope, CancellationToken cancellationToken);
    Task<LinkedStudentResponse> LinkStudentAsync(int parentId, LinkStudentRequest request, CancellationToken cancellationToken);
    /// <summary>Makes the parent the student's primary contact. 404 when the two are not linked.</summary>
    Task SetPrimaryAsync(int parentId, int studentId, CancellationToken cancellationToken);
}
