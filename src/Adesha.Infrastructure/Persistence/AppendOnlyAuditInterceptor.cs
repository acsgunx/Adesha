using Adesha.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Adesha.Infrastructure.Persistence;

/// <summary>
/// Enforces Rule 6 at the EF Core level: audit rows are append-only. Any attempt to
/// update or delete an <see cref="AuditRecord"/> through the application throws before
/// SQL is generated. (Database-level REVOKE hardening is added in Work Order 6.)
/// </summary>
public sealed class AppendOnlyAuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnsureAppendOnly(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnsureAppendOnly(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditRecord>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"Audit records are append-only; attempted to {entry.State} audit record {entry.Entity.Id}.");
            }
        }
    }
}
