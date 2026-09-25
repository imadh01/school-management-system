using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class ParentRepository : IParentRepository
{
    private readonly ApplicationDbContext _context;

    public ParentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<Parent>> GetAllAsync(CancellationToken cancellationToken) =>
        _context.Parents.OrderByDescending(p => p.Id).ToListAsync(cancellationToken);

    public Task<Parent?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.Parents.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(Parent parent, CancellationToken cancellationToken)
    {
        await _context.Parents.AddAsync(parent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
