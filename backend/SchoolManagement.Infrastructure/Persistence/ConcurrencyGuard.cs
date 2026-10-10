using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Infrastructure.Persistence;

public class ConcurrencyGuard : IConcurrencyGuard
{
    private readonly ApplicationDbContext _context;

    public ConcurrencyGuard(ApplicationDbContext context) => _context = context;

    public void Expect(IHasRowVersion entity, string rowVersion)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("The record version is not valid. Reload the record and try again.");
        }

        var entry = _context.Entry(entity);

        // The UPDATE will say "WHERE Id = @id AND RowVersion = <the version the client saw>".
        // If another save got there first, no row matches and EF throws DbUpdateConcurrencyException.
        entry.Property(nameof(IHasRowVersion.RowVersion)).OriginalValue = expected;

        // Make sure the main row is always part of the UPDATE, even when only child rows
        // (health, pickup persons, guardians...) changed - otherwise the check would be skipped.
        entry.Property(nameof(Student.UpdatedAt)).IsModified = true;
    }
}
