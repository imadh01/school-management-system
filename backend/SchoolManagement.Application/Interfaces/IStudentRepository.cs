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

/// <summary>A soft-deleted student for the Deleted list.</summary>
public record DeletedStudentRow(
    int Id, string AdmNo, string FirstName, string LastName, string ClassName, string ClassSection,
    string AcademicYearName, string Status, DateTime DeletedAt, string? DeletedByUsername);

public interface IStudentRepository
{
    /// <summary>Soft-deleted students, newest deletion first (query filters off).</summary>
    Task<List<DeletedStudentRow>> GetDeletedAsync(CancellationToken cancellationToken);
    /// <summary>One student whether or not deleted (query filters off), tracked, for Restore.</summary>
    Task<Student?> GetIncludingDeletedByIdAsync(int id, CancellationToken cancellationToken);
    /// <summary>True if the user account exists and is not deleted.</summary>
    Task<bool> LoginAccountIsLiveAsync(int userId, CancellationToken cancellationToken);
    /// <summary>True if another LIVE student is already linked to this user account.</summary>
    Task<bool> LoginAccountUsedByOtherStudentAsync(int userId, int excludeStudentId, CancellationToken cancellationToken);

    Task<List<Student>> GetAllAsync(CancellationToken cancellationToken);
    /// <summary>List rows, filtered to <paramref name="scope"/> inside the query (F1).</summary>
    Task<List<StudentSummaryRow>> GetSummariesAsync(DataScope scope, CancellationToken cancellationToken);
    /// <summary>
    /// How the caller reaches this student under <paramref name="scope"/> (F1).
    /// <see cref="StudentAccess.None"/> = missing OR out of scope; callers answer both with the same 404.
    /// </summary>
    Task<StudentAccess> GetAccessAsync(int studentId, DataScope scope, CancellationToken cancellationToken);
    Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken);
    /// <summary>Student with class, admission, health, identity and pickup persons loaded (tracked, for editing).</summary>
    Task<Student?> GetDetailByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsByAdmNoAsync(string admNo, CancellationToken cancellationToken);
    /// <summary>Highest serial among admission numbers starting with the prefix (0 if none). Deleted students count: their number stays reserved.</summary>
    Task<int> GetMaxAdmNoSerialAsync(string prefix, CancellationToken cancellationToken);
    /// <summary>Highest serial among roll numbers ever given in this section (any period status) that start with the prefix (0 if none).</summary>
    Task<int> GetMaxRollSerialAsync(int classSectionId, string prefix, CancellationToken cancellationToken);
    /// <summary>True if an ACTIVE enrollment in this class already uses the roll number (students who left do not hold one).</summary>
    Task<bool> ExistsByRollNumberInClassAsync(int classSectionId, string rollNumber, CancellationToken cancellationToken);
    /// <summary>True if the student has any enrollment period or attendance record; such a student must not be deleted.</summary>
    Task<bool> HasAcademicHistoryAsync(int studentId, CancellationToken cancellationToken);
    Task<bool> ExistsByEmiratesIdAsync(string emiratesIdNumber, int excludeStudentId, CancellationToken cancellationToken);
    Task<bool> ExistsByAadhaarAsync(string aadhaarNumber, int excludeStudentId, CancellationToken cancellationToken);
    /// <summary>All enrollment periods of a student, newest first (read-only projection).</summary>
    Task<List<StudentEnrollmentRow>> GetEnrollmentsAsync(int studentId, CancellationToken cancellationToken);
    /// <summary>Runs the operation in one database transaction; a unique-index violation becomes a 409.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
    Task AddAsync(Student student, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}