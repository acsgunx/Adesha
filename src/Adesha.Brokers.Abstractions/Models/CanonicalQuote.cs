using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

public sealed record LtpQuote(string Exchange, string TradingSymbol, long BrokerInstrumentToken, decimal LastPrice);

public sealed record OhlcQuote(
    string Exchange,
    string TradingSymbol,
    long BrokerInstrumentToken,
    decimal LastPrice,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close);
