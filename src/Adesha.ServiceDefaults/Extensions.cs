using Adesha.ServiceDefaults.Redaction;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddSerilogWithRedaction();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        // Master Prompt Rule 11: the stock Aspire template calls AddStandardResilienceHandler()
        // here, which silently retries ALL outbound HTTP — including a timed-out POST /orders
        // to a broker, i.e. a double-fill. We therefore do NOT apply any resilience handler by
        // default. Internal service-to-service clients opt in via AddInternalServiceResilience();
        // broker clients get hand-built pipelines with ZERO retries on mutating calls.
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Opt-in standard resilience (retry + circuit breaker + timeout) for INTERNAL,
    /// idempotent service-to-service clients only. Never apply this to a broker client.
    /// </summary>
    public static IHttpClientBuilder AddInternalServiceResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler();
        return builder;
    }

    private static TBuilder AddSerilogWithRedaction<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With(new RedactingEnricher())
                .Enrich.WithProperty("Application", "Adesha")
                .WriteTo.Console();

            // Export Serilog through OTLP so structured logs appear in the Aspire dashboard.
            var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                loggerConfiguration.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    var headers = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];
                    if (!string.IsNullOrWhiteSpace(headers))
                    {
                        foreach (var header in headers.Split(','))
                        {
                            var parts = header.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                options.Headers[parts[0]] = parts[1];
                            }
                        }
                    }
                });
            }
        });

        return builder;
    }

    private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(t =>
                        t.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation()
                    // Rule 3: scrub credential-bearing span attributes before export so broker
                    // API keys never reach the Aspire dashboard or an OTLP backend.
                    .AddProcessor(new CredentialRedactionProcessor());
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health endpoints are exposed in development only; production exposure is a
        // deliberate decision for Work Order 6 (security hardening).
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live"),
            });
        }

        return app;
    }
}
