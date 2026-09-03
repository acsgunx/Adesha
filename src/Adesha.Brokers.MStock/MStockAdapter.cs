using System.Globalization;
using System.Text.Json;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Errors;
using Adesha.Brokers.Abstractions.Models;
using Adesha.Brokers.MStock.Dtos;
using Adesha.Domain.Primitives;

namespace Adesha.Brokers.MStock;

/// <summary>
/// m.Stock by Mirae Asset broker adapter. Supports both API surfaces:
/// <list type="bullet">
/// <item><term>Type A</term><description>https://tradingapi.mstock.com/docs/v1/typeA/ — form-urlencoded auth.</description></item>
/// <item><term>Type B</term><description>https://tradingapi.mstock.com/docs/v1/typeB/ — JSON auth.</description></item>
/// </list>
///
/// Auth flow (both types): username/password -> OTP to registered mobile -> session/token (or
/// session/verifytotp when TOTP is enabled). TypeB additionally carries a refresh handle from
/// the login response into the session step. Tokens expire within 12 hours or at midnight,
/// whichever is first — they cannot be refreshed headlessly.
///
/// The type-specific differences (encoding, headers, request/response shapes) live in
/// <see cref="IMStockAuthStrategy"/>; the read path is shared between the two types.
/// </summary>
public sealed class MStockAdapter : IBrokerAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IMStockAuthStrategy _auth;
    private string? _accessToken;

    public MStockAdapter(HttpClient httpClient, MStockApiKey apiKey, MStockApiType apiType = MStockApiType.TypeA)
    {
        _httpClient = httpClient;
        _apiKey = apiKey.Value;
        _auth = apiType == MStockApiType.TypeB
            ? new MStockTypeBAuthStrategy()
            : new MStockTypeAAuthStrategy();
        _httpClient.BaseAddress = BuildBaseAddress(httpClient.BaseAddress, _auth.PathSegment);
    }

    private static Uri BuildBaseAddress(Uri? configuredBaseAddress, string pathSegment)
    {
        var authority = configuredBaseAddress?.GetLeftPart(UriPartial.Authority)
            ?? "https://api.mstock.trade";
        return new Uri($"{authority}/openapi/{pathSegment}/");
    }

    public BrokerId BrokerId => BrokerId.MStock;

    public BrokerCapabilities Capabilities { get; } = new()
    {
        BrokerId = BrokerId.MStock,
        DisplayName = "m.Stock by Mirae Asset",
        SupportsOtpLogin = true,
        SupportsTotpLogin = true,
        SupportsInstrumentMaster = true,
        SupportsLtpQuotes = true,
        SupportsOhlcQuotes = true,
        SupportsOrderBook = true,
        SupportsTradeBook = true,
        SupportsPositions = true,
        SupportsHoldings = true,
        SupportsFunds = true,
        SupportsOrderPlacement = false, // Work Order 3
        SupportsOrderModification = false, // Work Order 3
        SupportsOrderCancellation = false, // Work Order 3
        SupportsGttOrders = false,
        SupportsWebSocketFeed = true, // Work Order 4
        SupportedExchanges = ["NSE", "BSE", "NFO", "BFO", "CDS"],
        SupportedProducts = ["CNC", "NRML", "MIS", "MTF"],
        SupportedOrderTypes = ["MARKET", "LIMIT", "SL", "SL-M"],
    };

    public void SetSession(BrokerSession session)
    {
        _accessToken = session.AccessToken;
    }

    public async Task<BrokerLoginState> InitiateLoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        // Step 1: POST connect/login -> triggers OTP to registered mobile (TypeA) or
        // returns a refresh handle for the next step (TypeB).
        using var request = _auth.BuildLoginRequest(username, password);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw MStockErrorMapper.MapError(response, body);
        }

        var refreshHandle = _auth.ProcessLoginResponse(body);
        return new BrokerLoginState
        {
            BrokerId = BrokerId.MStock,
            Username = username,
            RefreshHandle = refreshHandle,
        };
    }

    public async Task<BrokerSession> CompleteLoginWithOtpAsync(BrokerLoginState state, string otp, CancellationToken cancellationToken)
    {
        // Step 2: POST session/token. TypeA sends api_key + request_token(=OTP) + checksum;
        // TypeB sends refreshToken (from login) + otp.
        using var request = _auth.BuildSessionTokenRequest(_apiKey, otp, state.RefreshHandle);

        var session = await SendSessionRequestAsync(request, cancellationToken);
        _accessToken = session.AccessToken;
        return session;
    }

    public async Task<BrokerSession> CompleteLoginWithTotpAsync(BrokerLoginState state, string totp, CancellationToken cancellationToken)
    {
        // TOTP path: POST session/verifytotp. Used when TOTP is enabled on the m.Stock
        // account (OTP is not sent in this case). TypeA sends api_key + totp; TypeB sends
        // refreshToken (from login) + totp.
        using var request = _auth.BuildVerifyTotpRequest(_apiKey, totp, state.RefreshHandle);

        var session = await SendSessionRequestAsync(request, cancellationToken);
        _accessToken = session.AccessToken;
        return session;
    }

    private async Task<BrokerSession> SendSessionRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw MStockErrorMapper.MapError(response, body);
        }

        return _auth.ParseSessionResponse(body);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "logout");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw MStockErrorMapper.MapError(response, body);
        }

        _accessToken = null;
    }

    public async Task<CanonicalFunds> GetFundsAsync(CancellationToken cancellationToken)
    {
        // GET /user/fundsummary
        var data = await GetAsync<List<MStockFundSummary>>("user/fundsummary", cancellationToken);
        var fund = data is { Count: > 0 } ? data[0] : new MStockFundSummary();

        return new CanonicalFunds
        {
            AvailableBalance = ParseMoney(fund.AvailableBalance),
            UtilizedAmount = ParseMoney(fund.AmountUtilized),
            ClearBalance = ParseMoney(fund.ClearBalance),
            Collateral = ParseMoney(fund.Collaterals),
        };
    }

    public async Task<IReadOnlyList<CanonicalInstrument>> GetInstrumentMasterAsync(CancellationToken cancellationToken)
    {
        // GET /instruments/scriptmaster — returns CSV, not JSON.
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "instruments/scriptmaster");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw MStockErrorMapper.MapError(response, body);
        }

        return ParseInstrumentMasterCsv(body);
    }

    public async Task<IReadOnlyList<LtpQuote>> GetLtpQuotesAsync(
        IEnumerable<string> exchangeSymbolPairs, CancellationToken cancellationToken)
    {
        // GET /instruments/quote/ltp?i=NSE:ACC-EQ&i=BSE:ACC-A
        var pairs = exchangeSymbolPairs.ToList();
        if (pairs.Count == 0)
        {
            return [];
        }

        var query = string.Join("&", pairs.Select(p => $"i={Uri.EscapeDataString(p)}"));
        var data = await GetAsync<Dictionary<string, MStockLtpEntry>>($"instruments/quote/ltp?{query}", cancellationToken);

        if (data is null)
        {
            return [];
        }

        return data.Select(kvp =>
        {
            var parts = kvp.Key.Split(':', 2);
            return new LtpQuote(
                parts.Length > 0 ? parts[0] : string.Empty,
                parts.Length > 1 ? parts[1] : kvp.Key,
                kvp.Value.InstrumentToken,
                kvp.Value.LastPrice);
        }).ToList();
    }

    public async Task<IReadOnlyList<OhlcQuote>> GetOhlcQuotesAsync(
        IEnumerable<string> exchangeSymbolPairs, CancellationToken cancellationToken)
    {
        // GET /instruments/quote/ohlc?i=NSE:ACC-EQ
        var pairs = exchangeSymbolPairs.ToList();
        if (pairs.Count == 0)
        {
            return [];
        }

        var query = string.Join("&", pairs.Select(p => $"i={Uri.EscapeDataString(p)}"));
        var data = await GetAsync<Dictionary<string, MStockOhlcEntry>>($"instruments/quote/ohlc?{query}", cancellationToken);

        if (data is null)
        {
            return [];
        }

        return data.Select(kvp =>
        {
            var parts = kvp.Key.Split(':', 2);
            var ohlc = kvp.Value.Ohlc;
            return new OhlcQuote(
                parts.Length > 0 ? parts[0] : string.Empty,
                parts.Length > 1 ? parts[1] : kvp.Key,
                kvp.Value.InstrumentToken,
                kvp.Value.LastPrice,
                ohlc?.Open ?? 0m,
                ohlc?.High ?? 0m,
                ohlc?.Low ?? 0m,
                ohlc?.Close ?? 0m);
        }).ToList();
    }

    public async Task<IReadOnlyList<CanonicalOrder>> GetOrderBookAsync(CancellationToken cancellationToken)
    {
        // GET /orders
        var data = await GetAsync<List<MStockOrder>>("orders", cancellationToken);
        if (data is null)
        {
            return [];
        }

        return data.Select(MapOrder).ToList();
    }

    public async Task<IReadOnlyList<CanonicalTrade>> GetTradeBookAsync(CancellationToken cancellationToken)
    {
        // GET /tradebook — uses a different field naming convention (uppercase) than /orders.
        var data = await GetAsync<List<MStockTradeBookEntry>>("tradebook", cancellationToken);
        if (data is null)
        {
            return [];
        }

        return data.Select(MapTradeBookEntry).ToList();
    }

    public async Task<IReadOnlyList<CanonicalPosition>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        // GET /portfolio/positions — returns { net: [...], day: [...] }
        var data = await GetAsync<MStockPositionData>("portfolio/positions", cancellationToken);
        if (data?.Net is null)
        {
            return [];
        }

        return data.Net.Select(MapPosition).ToList();
    }

    public async Task<IReadOnlyList<CanonicalHolding>> GetHoldingsAsync(CancellationToken cancellationToken)
    {
        // GET /portfolio/holdings
        var data = await GetAsync<List<MStockHolding>>("portfolio/holdings", cancellationToken);
        if (data is null)
        {
            return [];
        }

        return data.Select(MapHolding).ToList();
    }

    // --- Helpers ---

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            throw new BrokerException(BrokerErrorKind.AuthExpired, "No active broker session. Login required.");
        }

        var request = new HttpRequestMessage(method, path);
        // Auth header shape is type-specific (TypeA: "token api_key:jwtToken",
        // TypeB: "Bearer jwtToken" + X-PrivateKey).
        _auth.ApplyAuth(request, _apiKey, _accessToken);
        return request;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, path);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw MStockErrorMapper.MapError(response, body);
        }

        var result = JsonSerializer.Deserialize<MStockResponse<T>>(body, JsonOptions);
        if (result?.Status is "error" or "false")
        {
            throw MStockErrorMapper.MapBusinessError(result ?? new MStockResponse<T>());
        }

        return result?.Data;
    }

    private static Money ParseMoney(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)
            ? new Money(amount)
            : Money.Zero();

    private static CanonicalOrder MapOrder(MStockOrder o) => new()
    {
        BrokerOrderId = o.OrderId ?? string.Empty,
        TradingSymbol = o.TradingSymbol ?? string.Empty,
        Exchange = o.Exchange ?? string.Empty,
        TransactionType = o.TransactionType ?? string.Empty,
        OrderType = o.OrderType ?? string.Empty,
        Product = o.Product ?? string.Empty,
        Validity = o.Validity ?? string.Empty,
        Quantity = o.Quantity,
        DisclosedQuantity = o.DisclosedQuantity,
        Price = o.Price,
        TriggerPrice = o.TriggerPrice,
        AveragePrice = o.AveragePrice,
        FilledQuantity = o.FilledQuantity,
        PendingQuantity = o.PendingQuantity,
        CancelledQuantity = o.CancelledQuantity,
        Status = o.Status ?? string.Empty,
        StatusMessage = o.StatusMessage,
        OrderTimestamp = ParseTimestamp(o.OrderTimestamp),
        Variety = o.Variety,
    };

    private static CanonicalTrade MapTradeBookEntry(MStockTradeBookEntry t) => new()
    {
        TradeId = t.TradeNumber ?? string.Empty,
        BrokerOrderId = t.OrderNumber ?? string.Empty,
        Exchange = t.Exchange ?? string.Empty,
        TradingSymbol = t.Symbol ?? string.Empty,
        TransactionType = t.BuySell ?? string.Empty,
        Quantity = t.Quantity,
        AveragePrice = t.Price,
        FillTimestamp = ParseTimestamp(t.OrderDateTime) ?? DateTimeOffset.UtcNow,
        Product = t.Product ?? string.Empty,
    };

    private static CanonicalPosition MapPosition(MStockPosition p) => new()
    {
        TradingSymbol = p.TradingSymbol ?? string.Empty,
        Exchange = p.Exchange ?? string.Empty,
        BrokerInstrumentToken = p.InstrumentToken,
        Product = p.Product ?? string.Empty,
        Quantity = p.Quantity,
        OvernightQuantity = p.OvernightQuantity,
        AveragePrice = p.AveragePrice,
        ClosePrice = p.ClosePrice,
        LastPrice = p.LastPrice,
        Pnl = p.Pnl,
        MarkToMarket = p.M2m,
        BuyQuantity = p.BuyQuantity,
        BuyPrice = p.BuyPrice,
        BuyValue = p.BuyValue,
        SellQuantity = p.SellQuantity,
        SellPrice = p.SellPrice,
        SellValue = p.SellValue,
    };

    private static CanonicalHolding MapHolding(MStockHolding h) => new()
    {
        TradingSymbol = h.TradingSymbol ?? string.Empty,
        BrokerInstrumentToken = h.InstrumentToken,
        Isin = h.Isin ?? string.Empty,
        Quantity = h.Quantity,
        UsedQuantity = h.UsedQuantity,
        T1Quantity = h.T1Quantity,
        AveragePrice = h.AveragePrice,
        LastPrice = h.LastPrice,
        ClosePrice = h.ClosePrice,
        Pnl = h.Pnl,
        DayChange = h.DayChange,
        DayChangePercentage = h.DayChangePercentage,
    };

    private static DateTimeOffset? ParseTimestamp(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp))
        {
            return null;
        }

        // Order book uses "30-09-2024 15:45:46" (IST); trade history uses "2024-02-14 14:48:23".
        if (DateTimeOffset.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        if (DateTimeOffset.TryParseExact(timestamp, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    private static IReadOnlyList<CanonicalInstrument> ParseInstrumentMasterCsv(string csv)
    {
        // CSV header: instrument_token,exchange_token,tradingsymbol,name,last_price,expiry,strike,tick_size,lot_size,instrument_type,segment,exchange
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return [];
        }

        var instruments = new List<CanonicalInstrument>(lines.Length - 1);

        for (var i = 1; i < lines.Length; i++)
        {
            var fields = lines[i].Trim().Split(',');
            if (fields.Length < 12)
            {
                continue;
            }

            if (!long.TryParse(fields[0], out var instrumentToken))
            {
                continue;
            }

            decimal.TryParse(fields[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var lastPrice);
            decimal.TryParse(fields[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var tickSize);
            long.TryParse(fields[8], out var lotSize);
            DateOnly.TryParseExact(fields[5], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry);
            decimal.TryParse(fields[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var strike);

            instruments.Add(new CanonicalInstrument
            {
                InstrumentId = InstrumentId.New(),
                BrokerId = BrokerId.MStock,
                BrokerInstrumentToken = instrumentToken,
                TradingSymbol = fields[2],
                Exchange = fields[11],
                Name = fields[3],
                InstrumentType = fields[9],
                Segment = fields[10],
                TickSize = tickSize,
                LotSize = lotSize,
                Expiry = expiry == default ? null : expiry,
                StrikePrice = strike == 0 ? null : strike,
            });
        }

        return instruments;
    }
}
