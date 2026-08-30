using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

/// <summary>
/// Canonical instrument record, mapped from a broker's instrument master. The
/// InstrumentId is internal and stable; the broker's tradingsymbol is kept for
/// adapter-level routing but never used as a business key.
/// </summary>
public sealed class CanonicalInstrument
{
    public required InstrumentId InstrumentId { get; init; }
    public required BrokerId BrokerId { get; init; }

    /// <summary>Broker's instrument token (numeric, broker-specific).</summary>
    public required long BrokerInstrumentToken { get; init; }

    /// <summary>Broker's raw tradingsymbol (e.g. "INFY-EQ"). Used only for broker calls.</summary>
    public required string TradingSymbol { get; init; }

    public required string Exchange { get; init; }
    public required string Name { get; init; }
    public required string InstrumentType { get; init; }
    public required string Segment { get; init; }

    public decimal TickSize { get; init; }
    public long LotSize { get; init; }

    public DateOnly? Expiry { get; init; }
    public decimal? StrikePrice { get; init; }
}
