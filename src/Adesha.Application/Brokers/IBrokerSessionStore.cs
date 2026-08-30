using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;

namespace Adesha.Application.Brokers;

/// <summary>
/// Persists broker session metadata (not the raw access token) so the application
/// can track session expiry and prompt re-authentication before the token lapses.
/// The raw access token is held only in memory by the adapter; this store records
/// the expiry timestamp and user identity for UI/health-check purposes.
/// </summary>
public interface IBrokerSessionStore
{
    /// <summary>Saves session metadata after a successful broker login.</summary>
    Task SaveSessionAsync(BrokerSession session, CancellationToken cancellationToken);

    /// <summary>Returns the current session metadata, or null if no session exists.</summary>
    Task<BrokerSessionMetadata?> GetSessionAsync(BrokerId brokerId, CancellationToken cancellationToken);

    /// <summary>Clears the session after logout or expiry.</summary>
    Task ClearSessionAsync(BrokerId brokerId, CancellationToken cancellationToken);

    /// <summary>Returns true if the session exists and has not expired.</summary>
    Task<bool> IsSessionActiveAsync(BrokerId brokerId, CancellationToken cancellationToken);
}

/// <summary>
/// Persisted session metadata. The AccessToken is NOT stored here — only the
/// expiry and user identity. The raw token lives in the adapter's in-memory state.
/// </summary>
public sealed class BrokerSessionMetadata
{
    public required BrokerId BrokerId { get; init; }
    public required string UserId { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public IReadOnlyCollection<string> Exchanges { get; init; } = [];
    public IReadOnlyCollection<string> Products { get; init; } = [];
    public IReadOnlyCollection<string> OrderTypes { get; init; } = [];

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
}
