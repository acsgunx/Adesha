using System.Collections.Concurrent;
using System.Text.Json;
using Adesha.Application.Brokers;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;
using Adesha.Domain.Primitives;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Adesha.Infrastructure.Brokers;

/// <summary>
/// Caches the broker instrument master in Redis with a 24-hour TTL. The instrument
/// list is large (tens of thousands of entries) and changes once daily, so caching
/// is essential. The cache key includes the broker id and a date partition so that
/// a refresh mid-day does not invalidate in-flight lookups.
///
/// The canonical InstrumentId is generated on first fetch and persisted alongside
/// the instrument data in the cache. On subsequent refreshes, the same tradingsymbol
/// + exchange + broker combination gets the same InstrumentId by looking up the
/// previous mapping first. This ensures stable internal identifiers across daily
/// refreshes (Master Prompt Rule 10).
/// </summary>
public sealed class InstrumentMasterService : IInstrumentMasterService
{
    private const string CacheKeyPrefix = "adesha:instrument-master:";
    private const string IndexKeyPrefix = "adesha:instrument-index:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly IEnumerable<IBrokerAdapter> _adapters;
    private readonly IDistributedCache _cache;
    private readonly ILogger<InstrumentMasterService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // In-memory index for fast lookups: brokerId → (exchange:symbol → InstrumentId)
    private readonly ConcurrentDictionary<BrokerId, Dictionary<string, Guid>> _instrumentIdIndex = new();

    public InstrumentMasterService(
        IEnumerable<IBrokerAdapter> adapters,
        IDistributedCache cache,
        ILogger<InstrumentMasterService> logger)
    {
        _adapters = adapters;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CanonicalInstrument>> GetInstrumentsAsync(
        BrokerId brokerId, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(brokerId);
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            var instruments = JsonSerializer.Deserialize<List<CanonicalInstrument>>(cached, _jsonOptions);
            if (instruments is not null)
            {
                RebuildIndex(brokerId, instruments);
                return instruments;
            }
        }

        return await RefreshAsync(brokerId, cancellationToken);
    }

    public async Task<IReadOnlyList<CanonicalInstrument>> RefreshAsync(
        BrokerId brokerId, CancellationToken cancellationToken)
    {
        var adapter = _adapters.FirstOrDefault(a => a.BrokerId == brokerId)
            ?? throw new InvalidOperationException($"No broker adapter registered for {brokerId}.");

        if (!adapter.Capabilities.SupportsInstrumentMaster)
        {
            throw new NotSupportedException($"{adapter.Capabilities.DisplayName} does not support instrument master.");
        }

        _logger.LogInformation("Refreshing instrument master for {BrokerId}", brokerId);

        // Fetch the previous index so we can preserve InstrumentId mappings across refreshes.
        await LoadIndexAsync(brokerId, cancellationToken);

        var instruments = await adapter.GetInstrumentMasterAsync(cancellationToken);

        // Preserve existing InstrumentIds for instruments that still exist.
        var index = _instrumentIdIndex.GetOrAdd(brokerId, _ => []);
        foreach (var instrument in instruments)
        {
            var lookupKey = GetLookupKey(instrument.Exchange, instrument.TradingSymbol);
            if (index.TryGetValue(lookupKey, out var existingId))
            {
                // Reuse the existing InstrumentId for stability.
                instrument.InstrumentId = new InstrumentId(existingId);
            }
            else
            {
                index[lookupKey] = instrument.InstrumentId.Value;
            }
        }

        // Cache the instruments and the index.
        var cacheKey = GetCacheKey(brokerId);
        var json = JsonSerializer.Serialize(instruments, _jsonOptions);
        await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
        }, cancellationToken);

        await SaveIndexAsync(brokerId, cancellationToken);

        _logger.LogInformation("Instrument master refreshed for {BrokerId}: {Count} instruments",
            brokerId, instruments.Count);

        return instruments;
    }

    public async Task<CanonicalInstrument?> FindByTradingSymbolAsync(
        BrokerId brokerId, string exchange, string tradingSymbol, CancellationToken cancellationToken)
    {
        var instruments = await GetInstrumentsAsync(brokerId, cancellationToken);
        return instruments.FirstOrDefault(i =>
            i.Exchange.Equals(exchange, StringComparison.OrdinalIgnoreCase) &&
            i.TradingSymbol.Equals(tradingSymbol, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetCacheKey(BrokerId brokerId) => $"{CacheKeyPrefix}{brokerId}";
    private static string GetIndexKey(BrokerId brokerId) => $"{IndexKeyPrefix}{brokerId}";
    private static string GetLookupKey(string exchange, string tradingSymbol) =>
        $"{exchange.ToUpperInvariant()}:{tradingSymbol.ToUpperInvariant()}";

    private void RebuildIndex(BrokerId brokerId, IReadOnlyList<CanonicalInstrument> instruments)
    {
        var index = _instrumentIdIndex.GetOrAdd(brokerId, _ => []);
        foreach (var instrument in instruments)
        {
            index[GetLookupKey(instrument.Exchange, instrument.TradingSymbol)] = instrument.InstrumentId.Value;
        }
    }

    private async Task LoadIndexAsync(BrokerId brokerId, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(GetIndexKey(brokerId), cancellationToken);
        if (!string.IsNullOrEmpty(json))
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, Guid>>(json, _jsonOptions);
            if (loaded is not null)
            {
                _instrumentIdIndex[brokerId] = loaded;
            }
        }
    }

    private async Task SaveIndexAsync(BrokerId brokerId, CancellationToken cancellationToken)
    {
        if (_instrumentIdIndex.TryGetValue(brokerId, out var index))
        {
            var json = JsonSerializer.Serialize(index, _jsonOptions);
            await _cache.SetStringAsync(GetIndexKey(brokerId), json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl * 7, // Index lives longer than the data
            }, cancellationToken);
        }
    }
}
