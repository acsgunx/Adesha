using Serilog.Context;

namespace Adesha.Api.Middleware;

/// <summary>
/// Accepts an inbound X-Correlation-Id (or generates one), exposes it on the response,
/// and pushes it into the Serilog context so every log line for the request carries it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var supplied)
            && !string.IsNullOrWhiteSpace(supplied)
            ? supplied.ToString()
            : Guid.CreateVersion7().ToString("N");

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items[CorrelationIdMiddleware.HeaderName] as string ?? "unknown";
}
