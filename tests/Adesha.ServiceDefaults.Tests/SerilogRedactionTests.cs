using Adesha.ServiceDefaults.Redaction;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Adesha.ServiceDefaults.Tests;

public class SerilogRedactionTests
{
    private const string FakeJwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.c2lnbmF0dXJl";

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (Logger Logger, CollectingSink Sink) CreateLogger()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new RedactingEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    private static string Render(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        logEvent.RenderMessage(writer);
        var properties = string.Join(";", logEvent.Properties.Select(p => $"{p.Key}={p.Value}"));
        return writer.ToString() + properties;
    }

    [Theory]
    [InlineData("ApiKey")]
    [InlineData("api_secret")]
    [InlineData("Password")]
    [InlineData("AccessToken")]
    [InlineData("Authorization")]
    [InlineData("TotpSeed")]
    public void Properties_with_sensitive_names_are_redacted(string propertyName)
    {
        var (logger, sink) = CreateLogger();
        logger.Information("Value is {" + propertyName + "}", "super-secret-value");

        var rendered = Render(sink.Events.Single());
        Assert.DoesNotContain("super-secret-value", rendered);
        Assert.Contains(CredentialRedactor.RedactedValue, rendered);
    }

    [Fact]
    public void MStock_authorization_header_shape_is_redacted_from_values()
    {
        var (logger, sink) = CreateLogger();
        logger.Information("Sending header {HeaderValue}", $"token my_api_key:{FakeJwt}");

        var rendered = Render(sink.Events.Single());
        Assert.DoesNotContain("my_api_key", rendered);
        Assert.DoesNotContain(FakeJwt, rendered);
    }

    [Fact]
    public void Bearer_tokens_and_bare_jwts_are_redacted_from_values()
    {
        var (logger, sink) = CreateLogger();
        logger.Information("Header {A} and body {B}", $"Bearer {FakeJwt}", $"payload {FakeJwt} end");

        var rendered = Render(sink.Events.Single());
        Assert.DoesNotContain(FakeJwt, rendered);
    }

    [Fact]
    public void Nested_structured_values_are_redacted()
    {
        var (logger, sink) = CreateLogger();
        logger.Information("Request {@Request}", new { Url = "/login", ApiSecret = "nested-secret", Inner = new { Password = "deep-secret" } });

        var rendered = Render(sink.Events.Single());
        Assert.DoesNotContain("nested-secret", rendered);
        Assert.DoesNotContain("deep-secret", rendered);
        Assert.Contains("/login", rendered);
    }

    [Fact]
    public void Non_sensitive_values_pass_through_untouched()
    {
        var (logger, sink) = CreateLogger();
        logger.Information("Order {OrderId} for {Symbol}", "ORD123", "RELIANCE");

        var rendered = Render(sink.Events.Single());
        Assert.Contains("ORD123", rendered);
        Assert.Contains("RELIANCE", rendered);
        Assert.DoesNotContain(CredentialRedactor.RedactedValue, rendered);
    }
}
