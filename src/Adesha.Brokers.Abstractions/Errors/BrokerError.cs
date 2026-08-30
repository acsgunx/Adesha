namespace Adesha.Brokers.Abstractions.Errors;

/// <summary>
/// Canonical error taxonomy. Every adapter translates broker-specific errors into one of
/// these. The application layer never sees a broker's raw error_type string.
/// </summary>
public enum BrokerErrorKind
{
    AuthExpired,
    AuthFailed,
    RateLimited,
    InsufficientFunds,
    InvalidInstrument,
    MarketClosed,
    BrokerRejected,
    BrokerUnavailable,
    Timeout,
    Unknown,
}

public sealed class BrokerException(BrokerErrorKind kind, string message, string? brokerErrorCode = null)
    : Exception(message)
{
    public BrokerErrorKind Kind { get; } = kind;
    public string? BrokerErrorCode { get; } = brokerErrorCode;
}
