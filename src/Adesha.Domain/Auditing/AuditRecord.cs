namespace Adesha.Domain.Auditing;

/// <summary>
/// Immutable, append-only audit row (Master Prompt Rule 6). Written for every mutation
/// of orders, trades, positions, or credentials. No update or delete path may exist.
/// </summary>
public sealed class AuditRecord
{
    public long Id { get; init; }

    /// <summary>Who performed the action (application user id or "system").</summary>
    public required string Actor { get; init; }

    /// <summary>What happened, e.g. "Order.Placed", "BrokerCredential.Updated".</summary>
    public required string Action { get; init; }

    /// <summary>Entity type the action applied to.</summary>
    public required string EntityType { get; init; }

    /// <summary>Identifier of the affected entity.</summary>
    public required string EntityId { get; init; }

    /// <summary>JSON snapshot before the change; null for creates.</summary>
    public string? BeforeState { get; init; }

    /// <summary>JSON snapshot after the change; null for deletes.</summary>
    public string? AfterState { get; init; }

    /// <summary>Broker-assigned request/order id, when the action involved a broker call.</summary>
    public string? BrokerRequestId { get; init; }

    /// <summary>Correlation id propagated from the originating request.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>UTC timestamp of the action (Rule 7: all storage is UTC).</summary>
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
