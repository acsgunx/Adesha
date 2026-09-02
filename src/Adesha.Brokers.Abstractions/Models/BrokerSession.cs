using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

/// <summary>
/// Authenticated broker session. The session store persists the full session (including
/// the access token) so the adapter can be restored on each request. The backing cache
/// should be secured with authentication/TLS in production.
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
