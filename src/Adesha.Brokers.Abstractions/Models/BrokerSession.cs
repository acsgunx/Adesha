using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

/// <summary>
/// Authenticated broker session. The access token is held here only in memory; the
/// session store persists metadata (expiry, user id) but never the raw token in plaintext.
/// </summary>
public sealed class BrokerSession
{
    public required BrokerId BrokerId { get; init; }
    public required string AccessToken { get; init; }
    public required string UserId { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public IReadOnlyCollection<string> Exchanges { get; init; } = [];
    public IReadOnlyCollection<string> Products { get; init; } = [];
    public IReadOnlyCollection<string> OrderTypes { get; init; } = [];

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
}
