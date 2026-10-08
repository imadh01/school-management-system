using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>Raw columns for one list row; the class label is composed in the service from ClassSection.BuildDisplayName.</summary>
public record StudentSummaryRow(
    int Id, string AdmNo, string RollNumber, int ClassSectionId,
    string ClassName, string ClassSection, string AcademicYearName,
    DateOnly AdmissionDate, string Status, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? Mobile, string Category, bool TransportRequired,
    string? Nationality, string? CurriculumTrack, string? House, string? EalCode,
    string? Allergies, string? AdmissionRegNo, int? AdmissionId);

/// <summary>One enrollment period with the names needed to display it.</summary>
public record StudentEnrollmentRow(
    int Id, int AcademicYearId, string AcademicYearName,
    int ClassSectionId, string ClassName, string ClassSection,
    string RollNumber, DateOnly StartDate, DateOnly? EndDate, string Status, string? Remarks);

public interface IStudentRepository
{
    Task<List<Student>> GetAllAsync(CancellationToken cancellationToken);
    Task<List<StudentSummaryRow>> GetSummariesAsync(CancellationToken cancellationToken);
    Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken);
    /// <summary>Student with class, admission, health, identity and pickup persons loaded (tracked, for editing).</summary>
    Task<Student?> GetDetailByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsByAdmNoAsync(string admNo, CancellationToken cancellationToken);
    Task<bool> ExistsByRollNumberInClassAsync(int classSectionId, string rollNumber, CancellationToken cancellationToken);
    Task<bool> ExistsByAadhaarAsync(string aadhaarNumber, int excludeStudentId, CancellationToken cancellationToken);
    /// <summary>All enrollment periods of a student, newest first (read-only projection).</summary>
    Task<List<StudentEnrollmentRow>> GetEnrollmentsAsync(int studentId, CancellationToken cancellationToken);
    /// <summary>Runs the operation in one database transaction; a unique-index violation becomes a 409.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
    Task AddAsync(Student student, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
