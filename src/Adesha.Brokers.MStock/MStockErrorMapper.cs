using System.Net;
using Adesha.Brokers.Abstractions.Errors;
using Adesha.Brokers.MStock.Dtos;

namespace Adesha.Brokers.MStock;

/// <summary>
/// Maps m.Stock HTTP responses and error_type strings to the canonical error taxonomy.
/// m.Stock returns HTTP 200 with status:"error" for many business errors (e.g. invalid OTP),
/// so we inspect the body, not just the status code.
/// </summary>
internal static class MStockErrorMapper
{
    public static BrokerException MapError(HttpResponseMessage response, string? body)
    {
        var errorType = ExtractErrorType(body);
        var message = ExtractMessage(body);

        return (response.StatusCode, errorType) switch
        {
            (HttpStatusCode.Unauthorized, "TokenException") => new BrokerException(BrokerErrorKind.AuthExpired, message ?? "Access token expired or invalid.", errorType),
            (HttpStatusCode.Forbidden, "APIKeyException") => new BrokerException(BrokerErrorKind.AuthExpired, message ?? "API key suspended or expired.", errorType),
            (HttpStatusCode.TooManyRequests, _) => new BrokerException(BrokerErrorKind.RateLimited, message ?? "Rate limit exceeded.", errorType),
            (HttpStatusCode.ServiceUnavailable, _) or
            (HttpStatusCode.BadGateway, _) or
            (HttpStatusCode.GatewayTimeout, _) => new BrokerException(BrokerErrorKind.BrokerUnavailable, message ?? "Broker is unavailable.", errorType),
            (HttpStatusCode.RequestTimeout, _) => new BrokerException(BrokerErrorKind.Timeout, message ?? "Request timed out.", errorType),
            (_, "TokenException") => new BrokerException(BrokerErrorKind.AuthExpired, message ?? "Token error.", errorType),
            _ => new BrokerException(BrokerErrorKind.Unknown, message ?? $"Broker returned {(int)response.StatusCode} {response.StatusCode}.", errorType),
        };
    }

    public static BrokerException MapBusinessError<T>(MStockResponse<T> response) where T : class
    {
        var errorType = response.ErrorType;
        var message = response.Message ?? "Broker returned a business error.";

        return errorType switch
        {
            "TokenException" => new BrokerException(BrokerErrorKind.AuthExpired, message, errorType),
            "APIKeyException" => new BrokerException(BrokerErrorKind.AuthExpired, message, errorType),
            "InputException" when message.Contains("FUND", StringComparison.OrdinalIgnoreCase) =>
                new BrokerException(BrokerErrorKind.InsufficientFunds, message, errorType),
            "InputException" when message.Contains("not connected", StringComparison.OrdinalIgnoreCase) =>
                new BrokerException(BrokerErrorKind.MarketClosed, message, errorType),
            "InputException" => new BrokerException(BrokerErrorKind.BrokerRejected, message, errorType),
            "MiraeException" => new BrokerException(BrokerErrorKind.BrokerRejected, message, errorType),
            _ => new BrokerException(BrokerErrorKind.Unknown, message, errorType),
        };
    }

    private static string? ExtractErrorType(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(body, @"""error_type""\s*:\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractMessage(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(body, @"""message""\s*:\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }
}
