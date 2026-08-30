using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

public sealed class CanonicalHolding
{
    public required string TradingSymbol { get; init; }
    public required long BrokerInstrumentToken { get; init; }
    public required string Isin { get; init; }
    public required long Quantity { get; init; }
    public required long UsedQuantity { get; init; }
    public required long T1Quantity { get; init; }
    public required decimal AveragePrice { get; init; }
    public required decimal LastPrice { get; init; }
    public required decimal ClosePrice { get; init; }
    public required decimal Pnl { get; init; }
    public required decimal DayChange { get; init; }
    public required decimal DayChangePercentage { get; init; }
}
