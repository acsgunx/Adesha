using Adesha.Domain.Orders;
using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

public sealed class CanonicalOrder
{
    public required string BrokerOrderId { get; init; }
    public required string TradingSymbol { get; init; }
    public required string Exchange { get; init; }
    public required string TransactionType { get; init; } // BUY / SELL
    public required string OrderType { get; init; } // MARKET / LIMIT / SL / SL-M
    public required string Product { get; init; } // CNC / NRML / MIS / MTF
    public required string Validity { get; init; } // DAY / IOC
    public required long Quantity { get; init; }
    public required long DisclosedQuantity { get; init; }
    public required decimal Price { get; init; }
    public required decimal TriggerPrice { get; init; }
    public required decimal AveragePrice { get; init; }
    public required long FilledQuantity { get; init; }
    public required long PendingQuantity { get; init; }
    public required long CancelledQuantity { get; init; }
    public required string Status { get; init; }
    public required string? StatusMessage { get; init; }
    public required DateTimeOffset? OrderTimestamp { get; init; }
    public required string? Variety { get; init; }
}
