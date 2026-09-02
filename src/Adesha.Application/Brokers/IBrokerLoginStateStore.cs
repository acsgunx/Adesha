using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;

namespace Adesha.Application.Brokers;

/// <summary>
/// Holds the short-lived state between the broker login initiation and completion steps
/// (e.g. the m.Stock TypeB refresh handle). The state is keyed by user and broker so the
/// multi-step flow works across HTTP requests even though the adapter is resolved per request.
/// </summary>
public interface IBrokerLoginStateStore
{
    /// <summary>Saves the pending login state, overwriting any existing state for the user.</summary>
    Task SaveAsync(string userId, BrokerLoginState state, CancellationToken cancellationToken);

    /// <summary>Retrieves and removes the pending login state for the user.</summary>
    Task<BrokerLoginState?> PopAsync(string userId, BrokerId brokerId, CancellationToken cancellationToken);

    /// <summary>Deletes any pending login state for the user and broker.</summary>
    Task DeleteAsync(string userId, BrokerId brokerId, CancellationToken cancellationToken);
}
