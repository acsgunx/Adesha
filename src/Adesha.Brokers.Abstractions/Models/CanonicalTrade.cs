using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

public sealed class CanonicalTrade
{
    public required string TradeId { get; init; }
    public required string BrokerOrderId { get; init; }
    public required string Exchange { get; init; }
    public required string TradingSymbol { get; init; }
    public required string TransactionType { get; init; }
    public required long Quantity { get; init; }
    public required decimal AveragePrice { get; init; }
    public required DateTimeOffset FillTimestamp { get; init; }
    public required string Product { get; init; }
}
