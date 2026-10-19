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

    public Task<List<Parent>> GetAllAsync(DataScope scope, CancellationToken cancellationToken) =>
        WithAddresses(_context.Parents.AsNoTracking().VisibleTo(_context, scope))
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

    public Task<Parent?> GetVisibleByIdAsync(int id, DataScope scope, CancellationToken cancellationToken) =>
        _context.Parents
            .AsNoTracking()
            .VisibleTo(_context, scope)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<int> CountLinkedStudentsAsync(int parentId, CancellationToken cancellationToken) =>
        _context.StudentGuardians.CountAsync(
            g => g.ParentId == parentId && !g.Student.IsDeleted, cancellationToken);

    public Task<Parent?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        WithAddresses(_context.Parents).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    // G1a: addresses with their country and region names (split query: one parent, several addresses).
    private static IQueryable<Parent> WithAddresses(IQueryable<Parent> query) =>
        query
            .Include(p => p.Addresses).ThenInclude(a => a.Country)
            .Include(p => p.Addresses).ThenInclude(a => a.Region)
            .AsSplitQuery();

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

    public async Task AddAsync(Parent parent, CancellationToken cancellationToken)
    {
        await _context.Parents.AddAsync(parent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
