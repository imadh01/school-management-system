using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly ApplicationDbContext _context;

    public StudentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<Student> WithIncludes() =>
        _context.Students
            .Include(s => s.ClassSection)
        .ThenInclude(c => c.AcademicYear)
            .Include(s => s.Admission);

    public Task<List<Student>> GetAllAsync(CancellationToken cancellationToken) =>
        WithIncludes().OrderByDescending(s => s.Id).ToListAsync(cancellationToken);

    // List page: only the columns a row shows, no tracking, no side tables except Allergies.
    public Task<List<StudentSummaryRow>> GetSummariesAsync(CancellationToken cancellationToken) =>
        _context.Students
            .AsNoTracking()
            .OrderByDescending(s => s.Id)
            .Select(s => new StudentSummaryRow(
                s.Id, s.AdmNo, s.RollNumber, s.ClassSectionId,
                s.ClassSection.Name, s.ClassSection.Section, s.ClassSection.AcademicYear.Name,
                s.AdmissionDate, s.Status, s.PhotoUrl,
                s.FirstName, s.MiddleName, s.LastName, s.Gender, s.DateOfBirth,
                s.Mobile, s.Category, s.TransportRequired,
                s.Nationality, s.CurriculumTrack, s.House, s.EalCode,
                s.Health != null ? s.Health.Allergies : null,
                s.Admission != null ? s.Admission.RegNo : null,
                s.AdmissionId))
            .ToListAsync(cancellationToken);

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        WithIncludes().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Student?> GetDetailByIdAsync(int id, CancellationToken cancellationToken) =>
        WithIncludes()
            .Include(s => s.Health)
            .Include(s => s.IdentityDocument)
            .Include(s => s.PickupPersons)
            // Only the current period is needed for edits; full history has its own query.
            .Include(s => s.Enrollments.Where(e => e.Status == EnrollmentStatuses.Active))
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ExistsByAdmNoAsync(string admNo, CancellationToken cancellationToken) =>
        _context.Students.AnyAsync(s => s.AdmNo == admNo, cancellationToken);

    public Task<bool> ExistsByRollNumberInClassAsync(int classSectionId, string rollNumber, CancellationToken cancellationToken) =>
        _context.Students.AnyAsync(
            s => s.ClassSectionId == classSectionId && s.RollNumber == rollNumber,
            cancellationToken);

    public Task<bool> ExistsByAadhaarAsync(string aadhaarNumber, int excludeStudentId, CancellationToken cancellationToken) =>
        _context.StudentIdentityDocuments.AnyAsync(
            d => d.AadhaarNumber == aadhaarNumber && d.StudentId != excludeStudentId,
            cancellationToken);

    public Task<List<StudentEnrollmentRow>> GetEnrollmentsAsync(int studentId, CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.Id)
            .Select(e => new StudentEnrollmentRow(
                e.Id, e.AcademicYearId, e.AcademicYear.Name,
                e.ClassSectionId, e.ClassSection.Name, e.ClassSection.Section,
                e.RollNumber, e.StartDate, e.EndDate, e.Status, e.Remarks))
            .ToListAsync(cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task AddAsync(Student student, CancellationToken cancellationToken)
    {
        await _context.Students.AddAsync(student, cancellationToken);
        await SaveAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => SaveAsync(cancellationToken);

    // The friendly pre-checks in the service can race with another request; the unique indexes
    // (AdmNo, class + roll number, Aadhaar, one student per admission) are the final authority.
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "This student conflicts with a record just saved by someone else " +
                "(admission number, roll number, Aadhaar number or admission already in use). Reload and try again.");
        }
    }
}
