using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class ClassSectionRepository : IClassSectionRepository
{
    private readonly ApplicationDbContext _context;

    public ClassSectionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Newest year first, then LKG, UKG, Class 1..10 (null grade sorts as 0), then section.
    public Task<List<ClassSection>> GetAllAsync(CancellationToken cancellationToken) =>
        _context.ClassSections
            .Include(c => c.AcademicYear)
            .Include(c => c.ClassTeacher)
            .OrderByDescending(c => c.AcademicYear.StartDate)
            .ThenBy(c => c.Grade ?? 0)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Section)
            .ToListAsync(cancellationToken);

    public Task<ClassSection?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.ClassSections
            .Include(c => c.AcademicYear).Include(c => c.ClassTeacher)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int academicYearId, string name, string section, int? excludeId, CancellationToken cancellationToken) =>
        _context.ClassSections.AnyAsync(
            c => c.AcademicYearId == academicYearId
                 && c.Name == name
                 && c.Section == section
                 && c.Id != (excludeId ?? 0),
            cancellationToken);

    public Task<Dictionary<int, int>> GetEnrolledCountsAsync(CancellationToken cancellationToken) =>
        _context.Students
            .Where(s => s.Status == "Active")
            .GroupBy(s => s.ClassSectionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public Task<int> CountActiveStudentsAsync(int classSectionId, CancellationToken cancellationToken) =>
        _context.Students.CountAsync(
            s => s.ClassSectionId == classSectionId && s.Status == "Active",
            cancellationToken);

    // sp_getapplock is SQL Server's named lock. Owner = Transaction means it is released
    // automatically at COMMIT or ROLLBACK, so it can never be left behind by mistake.
    // It locks a *name* (not rows), so it works the same for every code path that gives out a seat.
    public async Task LockSeatsAsync(int classSectionId, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("LockSeatsAsync must be called inside a transaction.");

        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = 10000;",
            new object[] { result, new SqlParameter("@resource", $"ClassSectionSeats:{classSectionId}") },
            cancellationToken);

        // 0 / 1 = granted. Negative = timeout (-1), cancelled (-2), deadlock victim (-3), error (-999).
        if ((int)result.Value < 0)
            throw new ConflictException("This class is being updated by someone else right now. Please try again.");
    }

    public async Task<ClassSectionDependencies> GetDependenciesAsync(int classSectionId, CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .CountAsync(s => s.ClassSectionId == classSectionId, cancellationToken);
        var admissions = await _context.Admissions
            .CountAsync(a => a.AppliedForClassSectionId == classSectionId
                             || a.AllottedClassSectionId == classSectionId, cancellationToken);
        var subjects = await _context.Subjects
            .CountAsync(s => s.ClassSectionId == classSectionId, cancellationToken);

        return new ClassSectionDependencies(students, admissions, subjects);
    }

    public async Task AddAsync(ClassSection classSection, CancellationToken cancellationToken)
    {
        await _context.ClassSections.AddAsync(classSection, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
