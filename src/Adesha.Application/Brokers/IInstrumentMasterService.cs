using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;

namespace Adesha.Application.Brokers;

/// <summary>
/// Fetches and caches the broker instrument master. The instrument master is a
/// large payload (tens of thousands of instruments) that changes daily. It is
/// fetched once per day, cached in Redis, and served from cache on subsequent
/// lookups. The canonical mapping (broker tradingsymbol → InstrumentId) is
/// stable across daily refreshes — the same instrument gets the same InstrumentId.
/// </summary>
public interface IInstrumentMasterService
{
    /// <summary>
    /// Returns all instruments for the given broker, fetching from cache or
    /// the broker API if the cache is cold or stale.
    /// </summary>
    Task<IReadOnlyList<CanonicalInstrument>> GetInstrumentsAsync(BrokerId brokerId, CancellationToken cancellationToken);

    /// <summary>
    /// Forces a refresh of the instrument master from the broker API.
    /// Called by a scheduled job or manually when new listings are expected.
    /// </summary>
    Task<IReadOnlyList<CanonicalInstrument>> RefreshAsync(BrokerId brokerId, CancellationToken cancellationToken);

    /// <summary>
    /// Looks up a single instrument by its broker tradingsymbol and exchange.
    /// Returns null if not found.
    /// </summary>
    Task<CanonicalInstrument?> FindByTradingSymbolAsync(
        BrokerId brokerId, string exchange, string tradingSymbol, CancellationToken cancellationToken);
}
