using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;

namespace Adesha.Application.Brokers;

/// <summary>
/// Persists broker session state so the application can restore it on the adapter per
/// request, track session expiry, and prompt re-authentication before the token lapses.
/// The access token is stored in the configured distributed cache (Redis); deployments
/// should secure that cache with authentication/TLS in production.
/// </summary>
public interface IBrokerSessionStore
{
    /// <summary>Saves session state after a successful broker login.</summary>
    Task SaveSessionAsync(BrokerSession session, CancellationToken cancellationToken);

    /// <summary>Returns the current session state, or null if no session exists.</summary>
    Task<BrokerSession?> GetSessionAsync(BrokerId brokerId, CancellationToken cancellationToken);

    /// <summary>Clears the session after logout or expiry.</summary>
    Task ClearSessionAsync(BrokerId brokerId, CancellationToken cancellationToken);

    /// <summary>Returns true if the session exists and has not expired.</summary>
    Task<bool> IsSessionActiveAsync(BrokerId brokerId, CancellationToken cancellationToken);
}
