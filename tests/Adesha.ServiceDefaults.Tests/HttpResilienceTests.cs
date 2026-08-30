using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Adesha.ServiceDefaults.Tests;

/// <summary>
/// Rule 11: no outbound HttpClient may inherit an automatic retry handler by default.
/// A mutating broker call that fails must be attempted exactly once unless a client
/// explicitly opts in to resilience (internal, idempotent clients only).
/// </summary>
public class HttpResilienceTests
{
    private sealed class CountingHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(responseFactory());
        }
    }

    private static (HttpClient Client, CountingHandler Handler) BuildClient(
        bool optInResilience, Func<HttpResponseMessage> responseFactory)
    {
        var handler = new CountingHandler(responseFactory);
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();

        var clientBuilder = builder.Services.AddHttpClient("test-client")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        if (optInResilience)
        {
            clientBuilder.AddInternalServiceResilience();
        }

        var host = builder.Build();
        var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("test-client");
        return (client, handler);
    }

    [Fact]
    public async Task Default_client_attempts_a_failing_mutating_call_exactly_once()
    {
        var (client, handler) = BuildClient(
            optInResilience: false,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));

        var response = await client.PostAsync("https://broker.example.test/orders", new StringContent("{}"));

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task Default_client_attempts_a_failing_idempotent_call_exactly_once()
    {
        var (client, handler) = BuildClient(
            optInResilience: false,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        await client.GetAsync("https://broker.example.test/positions");

        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task Opted_in_internal_client_retries_transient_failures()
    {
        var (client, handler) = BuildClient(
            optInResilience: true,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        await client.GetAsync("https://internal.example.test/data");

        Assert.True(handler.Attempts > 1, $"Expected retries for opted-in client, got {handler.Attempts} attempt(s).");
    }

    [Fact]
    public async Task Standard_resilience_handler_retries_POST_too_which_is_why_brokers_must_never_get_it()
    {
        // This documents the hazard: the Aspire-standard handler does NOT exclude
        // non-idempotent methods. If a broker client inherited it, a timed-out
        // POST /orders would be resubmitted automatically.
        var (client, handler) = BuildClient(
            optInResilience: true,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        await client.PostAsync("https://internal.example.test/data", new StringContent("{}"));

        Assert.True(handler.Attempts > 1,
            "Expected the standard handler to retry POST; if this fails, re-evaluate Rule 11 assumptions for this package version.");
    }
}
