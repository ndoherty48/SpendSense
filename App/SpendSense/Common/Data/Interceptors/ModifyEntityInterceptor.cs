using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using SpendSense.Common.Interfaces;

namespace SpendSense.Common.Data.Interceptors;

public sealed class ModifyEntityInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        var entries = eventData.Context?.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach(var entry in entries ?? [])
        {
            if (entry.Entity is ITimestamped createdAt)
            {
                createdAt.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);   
    }
}