using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adesha.Brokers.MStock.Dtos;

// Internal DTOs matching m.Stock API response shapes. These never leave the adapter.
// Source: https://tradingapi.mstock.com/docs/v1/typeA/ and /typeB/

internal sealed class MStockResponse<T>
{
    // TypeA returns status as a string ("success"/"error"); TypeB mixes strings
    // ("true"/"false"/"error") and booleans (true). The converter normalizes both
    // so callers can compare against "error"/"true"/"false" uniformly.
    [JsonPropertyName("status")]
    [JsonConverter(typeof(StringOrBoolConverter))]
    public string? Status { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error_type")]
    public string? ErrorType { get; set; }
}

/// <summary>
/// Reads a JSON value that may be either a string or a boolean and normalizes it
/// to a lowercase string ("true"/"false" for booleans). m.Stock's TypeB API returns
/// <c>"status": true</c> (boolean) on some endpoints and <c>"status": "true"</c>
/// (string) on others.
/// </summary>
internal sealed class StringOrBoolConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            _ => reader.GetString(),
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

internal sealed class MStockLoginData
{
    [JsonPropertyName("ugid")]
    public string? Ugid { get; set; }

    [JsonPropertyName("cid")]
    public string? Cid { get; set; }

    [JsonPropertyName("nm")]
    public string? Name { get; set; }

    [JsonPropertyName("is_kyc")]
    public string? IsKyc { get; set; }

    [JsonPropertyName("is_error")]
    public string? IsError { get; set; }
}

internal sealed class MStockSessionData
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("broker")]
    public string? Broker { get; set; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("public_token")]
    public string? PublicToken { get; set; }

    [JsonPropertyName("login_time")]
    public string? LoginTime { get; set; }

    [JsonPropertyName("exchanges")]
    public List<string>? Exchanges { get; set; }

    [JsonPropertyName("products")]
    public List<string>? Products { get; set; }

    [JsonPropertyName("order_types")]
    public List<string>? OrderTypes { get; set; }
}

internal sealed class MStockFundSummary
{
    [JsonPropertyName("AVAILABLE_BALANCE")]
    public string? AvailableBalance { get; set; }

    [JsonPropertyName("AMOUNT_UTILIZED")]
    public string? AmountUtilized { get; set; }

    [JsonPropertyName("CLEAR_BALANCE")]
    public string? ClearBalance { get; set; }

    [JsonPropertyName("COLLATERALS")]
    public string? Collaterals { get; set; }
}

internal sealed class MStockOrder
{
    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("tradingsymbol")]
    public string? TradingSymbol { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("transaction_type")]
    public string? TransactionType { get; set; }

    [JsonPropertyName("order_type")]
    public string? OrderType { get; set; }

    [JsonPropertyName("product")]
    public string? Product { get; set; }

    [JsonPropertyName("validity")]
    public string? Validity { get; set; }

    [JsonPropertyName("quantity")]
    public long Quantity { get; set; }

    [JsonPropertyName("disclosed_quantity")]
    public long DisclosedQuantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("trigger_price")]
    public decimal TriggerPrice { get; set; }

    [JsonPropertyName("average_price")]
    public decimal AveragePrice { get; set; }

    [JsonPropertyName("filled_quantity")]
    public long FilledQuantity { get; set; }

    [JsonPropertyName("pending_quantity")]
    public long PendingQuantity { get; set; }

    [JsonPropertyName("cancelled_quantity")]
    public long CancelledQuantity { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("order_timestamp")]
    public string? OrderTimestamp { get; set; }

    [JsonPropertyName("variety")]
    public string? Variety { get; set; }

    [JsonPropertyName("instrument_token")]
    public long InstrumentToken { get; set; }
}

internal sealed class MStockTradeBookEntry
{
    [JsonPropertyName("TRADE_NUMBER")]
    public string? TradeNumber { get; set; }

    [JsonPropertyName("ORDER_NUMBER")]
    public string? OrderNumber { get; set; }

    [JsonPropertyName("EXCHANGE")]
    public string? Exchange { get; set; }

    [JsonPropertyName("SYMBOL")]
    public string? Symbol { get; set; }

    [JsonPropertyName("BUY_SELL")]
    public string? BuySell { get; set; }

    [JsonPropertyName("QUANTITY")]
    public long Quantity { get; set; }

    [JsonPropertyName("PRICE")]
    public decimal Price { get; set; }

    [JsonPropertyName("ORDER_DATE_TIME")]
    public string? OrderDateTime { get; set; }

    [JsonPropertyName("PRODUCT")]
    public string? Product { get; set; }
}

internal sealed class MStockTradeHistoryEntry
{
    [JsonPropertyName("trade_id")]
    public string? TradeId { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("tradingsymbol")]
    public string? TradingSymbol { get; set; }

    [JsonPropertyName("transaction_type")]
    public string? TransactionType { get; set; }

    [JsonPropertyName("quantity")]
    public long Quantity { get; set; }

    [JsonPropertyName("average_price")]
    public decimal AveragePrice { get; set; }

    [JsonPropertyName("fill_timestamp")]
    public string? FillTimestamp { get; set; }

    [JsonPropertyName("order_timestamp")]
    public string? OrderTimestamp { get; set; }

    [JsonPropertyName("product")]
    public string? Product { get; set; }
}

internal sealed class MStockPositionData
{
    [JsonPropertyName("net")]
    public List<MStockPosition>? Net { get; set; }

    [JsonPropertyName("day")]
    public List<MStockPosition>? Day { get; set; }
}

internal sealed class MStockPosition
{
    [JsonPropertyName("tradingsymbol")]
    public string? TradingSymbol { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("instrument_token")]
    public long InstrumentToken { get; set; }

    [JsonPropertyName("product")]
    public string? Product { get; set; }

    [JsonPropertyName("quantity")]
    public long Quantity { get; set; }

    [JsonPropertyName("overnight_quantity")]
    public long OvernightQuantity { get; set; }

    [JsonPropertyName("average_price")]
    public decimal AveragePrice { get; set; }

    [JsonPropertyName("close_price")]
    public decimal ClosePrice { get; set; }

    [JsonPropertyName("last_price")]
    public decimal LastPrice { get; set; }

    [JsonPropertyName("pnl")]
    public decimal Pnl { get; set; }

    [JsonPropertyName("m2m")]
    public decimal M2m { get; set; }

    [JsonPropertyName("buy_quantity")]
    public long BuyQuantity { get; set; }

    [JsonPropertyName("buy_price")]
    public decimal BuyPrice { get; set; }

    [JsonPropertyName("buy_value")]
    public decimal BuyValue { get; set; }

    [JsonPropertyName("sell_quantity")]
    public long SellQuantity { get; set; }

    [JsonPropertyName("sell_price")]
    public decimal SellPrice { get; set; }

    [JsonPropertyName("sell_value")]
    public decimal SellValue { get; set; }
}

internal sealed class MStockHolding
{
    [JsonPropertyName("tradingsymbol")]
    public string? TradingSymbol { get; set; }

    [JsonPropertyName("instrument_token")]
    public long InstrumentToken { get; set; }

    [JsonPropertyName("isin")]
    public string? Isin { get; set; }

    [JsonPropertyName("quantity")]
    public long Quantity { get; set; }

    [JsonPropertyName("used_quantity")]
    public long UsedQuantity { get; set; }

    [JsonPropertyName("t1_quantity")]
    public long T1Quantity { get; set; }

    [JsonPropertyName("average_price")]
    public decimal AveragePrice { get; set; }

    [JsonPropertyName("last_price")]
    public decimal LastPrice { get; set; }

    [JsonPropertyName("close_price")]
    public decimal ClosePrice { get; set; }

    [JsonPropertyName("pnl")]
    public decimal Pnl { get; set; }

    [JsonPropertyName("day_change")]
    public decimal DayChange { get; set; }

    [JsonPropertyName("day_change_percentage")]
    public decimal DayChangePercentage { get; set; }
}

internal sealed class MStockOhlcEntry
{
    [JsonPropertyName("instrument_token")]
    public long InstrumentToken { get; set; }

    [JsonPropertyName("last_price")]
    public decimal LastPrice { get; set; }

    [JsonPropertyName("ohlc")]
    public MStockOhlc? Ohlc { get; set; }
}

internal sealed class MStockOhlc
{
    [JsonPropertyName("open")]
    public decimal Open { get; set; }

    [JsonPropertyName("high")]
    public decimal High { get; set; }

    [JsonPropertyName("low")]
    public decimal Low { get; set; }

    [JsonPropertyName("close")]
    public decimal Close { get; set; }
}

internal sealed class MStockLtpEntry
{
    [JsonPropertyName("instrument_token")]
    public long InstrumentToken { get; set; }

    [JsonPropertyName("last_price")]
    public decimal LastPrice { get; set; }
}

// --- Type B auth DTOs (Source: https://tradingapi.mstock.com/docs/v1/typeB/User/) ---

/// <summary>
/// TypeB <c>connect/login</c> response. The <c>jwtToken</c> field here is a short
/// refresh handle (UUID-like) that must be carried into <c>session/token</c> or
/// <c>session/verifytotp</c> as the <c>refreshToken</c> parameter.
/// </summary>
internal sealed class MStockTypeBLoginData
{
    [JsonPropertyName("jwtToken")]
    public string? JwtToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("feedToken")]
    public string? FeedToken { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

/// <summary>
/// TypeB <c>session/token</c> / <c>session/verifytotp</c> response. Here
/// <c>jwtToken</c> is the full JWT access token used as <c>Authorization: Bearer</c>.
/// </summary>
internal sealed class MStockTypeBSessionData
{
    [JsonPropertyName("jwtToken")]
    public string? JwtToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("feedToken")]
    public string? FeedToken { get; set; }

    [JsonPropertyName("ClientName")]
    public string? ClientName { get; set; }

    [JsonPropertyName("ClientId")]
    public string? ClientId { get; set; }

    [JsonPropertyName("exchanges")]
    public List<string>? Exchanges { get; set; }
}
