using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AdmissionRepository : IAdmissionRepository
{
    private readonly ApplicationDbContext _context;

    public AdmissionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Split query: Guardians is a collection, so a single JOIN would repeat every admission row per guardian.
    private IQueryable<Admission> WithIncludes() =>
        _context.Admissions
            .Include(a => a.AcademicYear)
            .Include(a => a.AppliedForClassSection)
            .Include(a => a.AllottedClassSection)
            .Include(a => a.Student)
            .Include(a => a.Guardians)
            .AsSplitQuery();

    public Task<List<Admission>> GetAllAsync(CancellationToken cancellationToken) =>
        WithIncludes().OrderByDescending(a => a.Id).ToListAsync(cancellationToken);

    public Task<Admission?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        WithIncludes().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Admission admission, CancellationToken cancellationToken)
    {
        await _context.Admissions.AddAsync(admission, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="operation"/> in one database transaction: if it throws, everything
    /// it saved is rolled back. A unique-index violation (two staff enrolling the same
    /// application, same admission number, ...) becomes a 409 instead of a 500.
    /// </summary>
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                throw new ConflictException(
                    "This record conflicts with one just saved by someone else " +
                    "(for example the same admission number or roll number). Reload and try again.");
            }
        });
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);

    // The join happens in SQL on the two persisted MobileKey columns, so "same number"
    // is defined in exactly one place (MobileKeySql).
    public Task<List<ParentCandidateRow>> GetParentCandidatesAsync(int admissionId, CancellationToken cancellationToken) =>
        (from g in _context.AdmissionGuardians
         where g.AdmissionId == admissionId && g.MobileKey != null && g.MobileKey != ""
         join p in _context.Parents on g.MobileKey equals p.MobileKey
         orderby g.Id, p.Id
         select new ParentCandidateRow(g.Id, p.Id, p.Name, p.Mobile, p.Email))
        .ToListAsync(cancellationToken);

    public Task<List<ParentChildRow>> GetLinkedChildrenAsync(IReadOnlyCollection<int> parentIds, CancellationToken cancellationToken) =>
        _context.StudentGuardians
            .Where(sg => parentIds.Contains(sg.ParentId))
            .Select(sg => new ParentChildRow(sg.ParentId, sg.Student.FirstName + " " + sg.Student.LastName))
            .ToListAsync(cancellationToken);
}
