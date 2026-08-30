using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

public sealed class CanonicalPosition
{
    public required string TradingSymbol { get; init; }
    public required string Exchange { get; init; }
    public required long BrokerInstrumentToken { get; init; }
    public required string Product { get; init; }
    public required long Quantity { get; init; }
    public required long OvernightQuantity { get; init; }
    public required decimal AveragePrice { get; init; }
    public required decimal ClosePrice { get; init; }
    public required decimal LastPrice { get; init; }
    public required decimal Pnl { get; init; }
    public required decimal MarkToMarket { get; init; }
    public required long BuyQuantity { get; init; }
    public required decimal BuyPrice { get; init; }
    public required decimal BuyValue { get; init; }
    public required long SellQuantity { get; init; }
    public required decimal SellPrice { get; init; }
    public required decimal SellValue { get; init; }
}
