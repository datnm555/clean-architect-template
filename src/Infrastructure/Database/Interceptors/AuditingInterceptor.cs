using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SharedKernel;

namespace Infrastructure.Database.Interceptors;

/// <summary>
/// Stamps CreatedAt / ModifiedAt on AuditedEntity instances at SaveChanges time.
/// Uses EF's PropertyEntry API which can set internal setters via model metadata.
/// </summary>
internal sealed class AuditingInterceptor(IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        DbContext? context = eventData.Context;
        if (context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        DateTime now = clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<AuditedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(AuditedEntity.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(AuditedEntity.ModifiedAt)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(AuditedEntity.ModifiedAt)).CurrentValue = now;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
