using System.Text.Json;
using Adesha.Application.Brokers;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace Adesha.Infrastructure.Brokers;

/// <summary>
/// Redis-backed store for the short-lived state between broker login initiation and
/// completion. Entries expire after a short window so abandoned login attempts do not
/// accumulate.
/// </summary>
public sealed class RedisBrokerLoginStateStore : IBrokerLoginStateStore
{
    private const string KeyPrefix = "adesha:broker-login:";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RedisBrokerLoginStateStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task SaveAsync(string userId, BrokerLoginState state, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl };
        await _cache.SetStringAsync(GetKey(userId, state.BrokerId), json, options, cancellationToken);
    }

    public async Task<BrokerLoginState?> PopAsync(string userId, BrokerId brokerId, CancellationToken cancellationToken)
    {
        var key = GetKey(userId, brokerId);
        var json = await _cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        await _cache.RemoveAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<BrokerLoginState>(json, _jsonOptions);
    }

    public Task DeleteAsync(string userId, BrokerId brokerId, CancellationToken cancellationToken)
    {
        return _cache.RemoveAsync(GetKey(userId, brokerId), cancellationToken);
    }

    private static string GetKey(string userId, BrokerId brokerId) => $"{KeyPrefix}{userId}:{brokerId}";
}
