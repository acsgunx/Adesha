using System.Text.Json;
using Adesha.Application.Brokers;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace Adesha.Infrastructure.Brokers;

/// <summary>
/// Redis-backed broker session store. The session metadata (expiry, user id) is
/// stored in Redis with a TTL matching the session expiry, so stale sessions are
/// automatically evicted. The raw access token is NOT stored — it lives only in
/// the adapter's in-memory state and is restored via SetSession() on restart.
/// </summary>
public sealed class RedisBrokerSessionStore : IBrokerSessionStore
{
    private const string KeyPrefix = "adesha:broker-session:";
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RedisBrokerSessionStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task SaveSessionAsync(BrokerSession session, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(session, _jsonOptions);
        var key = GetKey(session.BrokerId);

        // Set the Redis TTL to match the session expiry so stale sessions auto-evict.
        var ttl = session.ExpiresAtUtc - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            // Session already expired — don't store it.
            return;
        }

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        };

        await _cache.SetStringAsync(key, json, options, cancellationToken);
    }

    public async Task<BrokerSession?> GetSessionAsync(BrokerId brokerId, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(GetKey(brokerId), cancellationToken);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BrokerSession>(json, _jsonOptions);
    }

    public async Task ClearSessionAsync(BrokerId brokerId, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(GetKey(brokerId), cancellationToken);
    }

    public async Task<bool> IsSessionActiveAsync(BrokerId brokerId, CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(brokerId, cancellationToken);
        return session is { IsExpired: false };
    }

    private static string GetKey(BrokerId brokerId) => $"{KeyPrefix}{brokerId}";
}
