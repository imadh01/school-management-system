using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps CreatedAt/UpdatedAt (UTC) on save. Only User carries these
/// columns in Module 1 — extend the switch as later modules' entities
/// need the same treatment.
/// </summary>
public class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var now = DateTime.UtcNow;

            foreach (var entry in eventData.Context.ChangeTracker.Entries<User>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}