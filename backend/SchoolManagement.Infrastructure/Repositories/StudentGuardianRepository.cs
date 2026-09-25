using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
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

    public Task<List<StudentGuardian>> GetForParentAsync(int parentId, CancellationToken cancellationToken) =>
        _context.StudentGuardians
            .Include(sg => sg.Student).ThenInclude(s => s.ClassSection).ThenInclude(c => c.AcademicYear)
            .Where(sg => sg.ParentId == parentId)
            .ToListAsync(cancellationToken);

    public Task<bool> LinkExistsAsync(int studentId, int parentId, CancellationToken cancellationToken) =>
        _context.StudentGuardians.AnyAsync(
            sg => sg.StudentId == studentId && sg.ParentId == parentId, cancellationToken);

    public async Task AddLinkAsync(StudentGuardian link, CancellationToken cancellationToken)
    {
        await _context.StudentGuardians.AddAsync(link, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<StudentGuardian?> GetLinkAsync(int studentId, int parentId, CancellationToken cancellationToken) =>
        _context.StudentGuardians.FirstOrDefaultAsync(
            sg => sg.StudentId == studentId && sg.ParentId == parentId, cancellationToken);

    public async Task RemoveLinkAsync(StudentGuardian link, CancellationToken cancellationToken)
    {
        _context.StudentGuardians.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPrimaryAsync(int studentId, int parentId, CancellationToken cancellationToken)
    {
        var links = await _context.StudentGuardians
            .Where(sg => sg.StudentId == studentId)
            .ToListAsync(cancellationToken);

        foreach (var link in links)
            link.IsPrimaryContact = link.ParentId == parentId;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
