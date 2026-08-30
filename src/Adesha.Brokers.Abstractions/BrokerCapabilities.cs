namespace Adesha.Brokers.Abstractions;

/// <summary>
/// Declares what a broker supports. The application layer queries this before offering a
/// feature; unsupported features are hidden in the UI, never thrown as NotImplemented.
/// </summary>
public sealed class BrokerCapabilities
{
    public required BrokerId BrokerId { get; init; }
    public required string DisplayName { get; init; }

    public bool SupportsOtpLogin { get; init; }
    public bool SupportsTotpLogin { get; init; }
    public bool SupportsInstrumentMaster { get; init; }
    public bool SupportsLtpQuotes { get; init; }
    public bool SupportsOhlcQuotes { get; init; }
    public bool SupportsOrderBook { get; init; }
    public bool SupportsTradeBook { get; init; }
    public bool SupportsPositions { get; init; }
    public bool SupportsHoldings { get; init; }
    public bool SupportsFunds { get; init; }
    public bool SupportsOrderPlacement { get; init; }
    public bool SupportsOrderModification { get; init; }
    public bool SupportsOrderCancellation { get; init; }
    public bool SupportsGttOrders { get; init; }
    public bool SupportsWebSocketFeed { get; init; }

    public IReadOnlyCollection<string> SupportedExchanges { get; init; } = [];
    public IReadOnlyCollection<string> SupportedProducts { get; init; } = [];
    public IReadOnlyCollection<string> SupportedOrderTypes { get; init; } = [];
}
