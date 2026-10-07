using SchoolManagement.Application.DTOs.Teachers;

namespace SchoolManagement.Application.Interfaces;

public interface ITeacherService
{
    Task<List<TeacherResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<TeacherResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<TeacherResponse> CreateAsync(CreateTeacherRequest request, CancellationToken cancellationToken);
    Task<TeacherResponse> UpdateAsync(int id, UpdateTeacherRequest request, CancellationToken cancellationToken);
    Task<TeacherResponse> ChangeStatusAsync(int id, string status, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);

    Task<List<TeacherAssignmentResponse>> GetAssignmentsAsync(int id, CancellationToken cancellationToken);
    Task<List<TeacherAssignmentResponse>> AssignSubjectsAsync(int id, AssignSubjectsRequest request, CancellationToken cancellationToken);
    Task<List<TeacherAssignmentResponse>> UnassignSubjectAsync(int id, int subjectId, CancellationToken cancellationToken);
    Task<List<TeacherAssignmentResponse>> SetClassTeacherAsync(int id, int classSectionId, CancellationToken cancellationToken);
    Task<List<TeacherAssignmentResponse>> ClearClassTeacherAsync(int id, int classSectionId, CancellationToken cancellationToken);
}
