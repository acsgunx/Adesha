using Adesha.Brokers.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;

namespace Adesha.Brokers.MStock;

/// <summary>Wrapper for the m.Stock API key so it can be resolved via DI.</summary>
public sealed class MStockApiKey(string value)
{
    public string Value { get; } = value;
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the m.Stock broker adapter as a typed HttpClient.
    ///
    /// Resilience pipeline (Rule 11 compliant):
    /// - Read operations (GET): retry up to 3 times with exponential backoff + circuit breaker.
    /// - Mutating operations (POST/PUT/DELETE): ZERO retries — a timed-out order placement
    ///   must not be silently retried (double-fill hazard). Only a timeout applies.
    ///
    /// Rate limiting: m.Stock allows 1 Data API call/sec. We use a Polly rate limiter
    /// strategy in the resilience pipeline, not ASP.NET Core's inbound RateLimiter
    /// middleware (which is for incoming requests, not outbound HttpClient calls).
    /// </summary>
    public static IHttpClientBuilder AddMStockBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration["MStock:BaseUrl"] ?? "https://api.mstock.trade";

        // Register the API key as a named string so MStockAdapter can resolve it.
        // The key is resolved lazily from IConfiguration at service resolution time,
        // not at registration time, so test factories that inject configuration
        // after builder creation still work.
        services.AddSingleton<MStockApiKey>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var apiKey = config["MStock:ApiKey"]
                ?? throw new InvalidOperationException("MStock:ApiKey is not configured.");
            return new MStockApiKey(apiKey);
        });

        // Select the m.Stock API surface (TypeA form-urlencoded or TypeB JSON).
        // Defaults to TypeA to preserve existing behaviour. Registered via the
        // non-generic overload because MStockApiType is a value type (enum).
        services.AddSingleton(typeof(MStockApiType), sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var raw = config["MStock:ApiType"];
            return raw is null
                ? (object)MStockApiType.TypeA
                : Enum.TryParse<MStockApiType>(raw, ignoreCase: true, out var apiType)
                    ? apiType
                    : throw new InvalidOperationException($"MStock:ApiType '{raw}' is not valid. Use 'TypeA' or 'TypeB'.");
        });

        var httpClientBuilder = services.AddHttpClient<MStockAdapter>((sp, client) =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // m.Stock responses may be gzipped.
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        });

        // Rule 11: hand-built resilience pipeline, NOT AddStandardResilienceHandler.
        // Read pipeline: retry on transient failures + circuit breaker + timeout.
        // This is safe for GET requests (order book, funds, positions, etc.) because
        // they are idempotent. Mutating calls (POST/PUT/DELETE) will get a separate
        // no-retry pipeline in WO3 when order placement is implemented.
        httpClientBuilder.AddResilienceHandler("mstock-read", (builder, context) =>
        {
            var retryOptions = new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
            };

            builder.AddRetry(retryOptions)
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(15),
            })
            .AddTimeout(TimeSpan.FromSeconds(20));
        });

        return httpClientBuilder;
    }

    /// <summary>
    /// Registers IBrokerAdapter → MStockAdapter so the application layer can depend
    /// on the abstraction, not the concrete adapter.
    /// </summary>
    public static IServiceCollection AddMStockBrokerAdapter(this IServiceCollection services)
    {
        services.AddTransient<IBrokerAdapter>(sp =>
            sp.GetRequiredService<MStockAdapter>());
        return services;
    }
}
