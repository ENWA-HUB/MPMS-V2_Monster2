using MAIPT.PM.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MAIPT.PM.Api.Data;

/// <summary>
/// Central audit stamping for all AuditableEntity records.
/// Avoids overriding SaveChanges in AppDbContext, so it coexists with
/// any existing SaveChanges/SaveChangesAsync implementation.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static void Apply(DbContext? db)
    {
        if (db is null) return;

        var actorId = AuditActor.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in db.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                // Server-side ownership: never trust audit IDs supplied by the client.
                entry.Entity.CreatedByUserId = actorId;
                entry.Entity.UpdatedByUserId = actorId;
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Original creator/time are immutable.
                entry.Property(x => x.CreatedByUserId).IsModified = false;
                entry.Property(x => x.CreatedAt).IsModified = false;

                entry.Entity.UpdatedByUserId = actorId;
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
