using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class StudentGuardianRepository : IStudentGuardianRepository
{
    private readonly ApplicationDbContext _context;

    public StudentGuardianRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<StudentGuardian>> GetForStudentAsync(int studentId, CancellationToken cancellationToken) =>
        _context.StudentGuardians
            .Include(sg => sg.Parent)
            .Where(sg => sg.StudentId == studentId)
            .ToListAsync(cancellationToken);

    public Task<List<StudentGuardian>> GetForParentAsync(int parentId, DataScope studentScope, CancellationToken cancellationToken)
    {
        // A teacher opening a parent sees only the children they teach, not siblings in other classes.
        var visibleStudentIds = _context.Students.VisibleTo(_context, studentScope).Select(s => s.Id);
        return _context.StudentGuardians
            .AsNoTracking()
            .Include(sg => sg.Student).ThenInclude(s => s.ClassSection).ThenInclude(c => c.AcademicYear)
            .Where(sg => sg.ParentId == parentId && visibleStudentIds.Contains(sg.StudentId))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> LinkExistsAsync(int studentId, int parentId, CancellationToken cancellationToken) =>
        _context.StudentGuardians.AnyAsync(
            sg => sg.StudentId == studentId && sg.ParentId == parentId, cancellationToken);

    public async Task AddLinkAsync(StudentGuardian link, CancellationToken cancellationToken)
    {
        await _context.StudentGuardians.AddAsync(link, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The service checks first; this is the answer when someone else saved between the check and now
            // (the same pair, or another primary contact for the same student).
            throw new ConflictException(
                "This guardian link conflicts with one just saved by someone else " +
                "(already linked, or the student just got a primary contact). Reload and try again.");
        }
    }

    public Task<StudentGuardian?> GetLinkAsync(int studentId, int parentId, CancellationToken cancellationToken) =>
        _context.StudentGuardians.FirstOrDefaultAsync(
            sg => sg.StudentId == studentId && sg.ParentId == parentId, cancellationToken);

    public async Task RemoveLinkAsync(StudentGuardian link, CancellationToken cancellationToken)
    {
        _context.StudentGuardians.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SetPrimaryAsync(int studentId, int parentId, CancellationToken cancellationToken)
    {
        // Two saves in ONE transaction. The unique index UX_StudentGuardians_Student_Primary allows a single
        // primary per student and SQL Server checks it per statement, so the old primary must be cleared
        // BEFORE the new one is set (one SaveChanges would order the two updates by key, not by need).
        // If anything fails, the transaction rolls back and the old primary is still in place.
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var links = await _context.StudentGuardians
                .Where(sg => sg.StudentId == studentId)
                .ToListAsync(cancellationToken);

            var target = links.FirstOrDefault(l => l.ParentId == parentId);
            if (target is null) return false; // nothing changed: the current primary stays

            foreach (var other in links.Where(l => l.ParentId != parentId && l.IsPrimaryContact))
                other.IsPrimaryContact = false;
            await _context.SaveChangesAsync(cancellationToken); // frees the one primary slot

            if (!target.IsPrimaryContact)
            {
                target.IsPrimaryContact = true;
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }
}
