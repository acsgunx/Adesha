using System.Net;
using System.Text.Json;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Errors;
using Adesha.Brokers.Abstractions.Models;
using Adesha.Brokers.MStock.Dtos;
using Xunit;

namespace Adesha.Brokers.MStock.Tests;

public class MStockAdapterTests
{
    private const string ApiKey = "test-api-key";
    private const string AccessToken = "test-access-token";

    private static MStockAdapter CreateAdapter(Func<HttpRequestMessage, HttpResponseMessage> handler, MStockApiType apiType = MStockApiType.TypeA)
    {
        var httpClient = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri("https://api.mstock.trade"),
        };
        return new MStockAdapter(httpClient, new MStockApiKey(ApiKey), apiType);
    }

    private static void SetSession(MStockAdapter adapter)
    {
        adapter.SetSession(new BrokerSession
        {
            BrokerId = BrokerId.MStock,
            AccessToken = AccessToken,
            UserId = "538",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(10),
        });
    }

    [Fact]
    public void BrokerId_IsMStock()
    {
        var adapter = CreateAdapter(_ => new HttpResponseMessage());
        Assert.Equal(BrokerId.MStock, adapter.BrokerId);
    }

    [Fact]
    public void Capabilities_DeclaresReadOperationsOnly()
    {
        var adapter = CreateAdapter(_ => new HttpResponseMessage());
        var caps = adapter.Capabilities;

        Assert.True(caps.SupportsOtpLogin);
        Assert.True(caps.SupportsTotpLogin);
        Assert.True(caps.SupportsInstrumentMaster);
        Assert.True(caps.SupportsLtpQuotes);
        Assert.True(caps.SupportsOhlcQuotes);
        Assert.True(caps.SupportsOrderBook);
        Assert.True(caps.SupportsTradeBook);
        Assert.True(caps.SupportsPositions);
        Assert.True(caps.SupportsHoldings);
        Assert.True(caps.SupportsFunds);
        // WO2: no order mutation
        Assert.False(caps.SupportsOrderPlacement);
        Assert.False(caps.SupportsOrderModification);
        Assert.False(caps.SupportsOrderCancellation);
    }

    [Fact]
    public async Task InitiateLoginAsync_SendsFormEncodedCredentials()
    {
        string? capturedContent = null;
        var adapter = CreateHandlerAdapter(req =>
        {
            capturedContent = req.Content?.ReadAsStringAsync().Result;
            return Ok(new { status = "success", data = new { ugid = "abc", cid = "538", is_error = "false" } });
        });

        var state = await adapter.InitiateLoginAsync("myuser", "mypass", CancellationToken.None);

        Assert.NotNull(capturedContent);
        Assert.Contains("username=myuser", capturedContent);
        Assert.Contains("password=mypass", capturedContent);
        Assert.Equal("myuser", state.Username);
        Assert.Equal(BrokerId.MStock, state.BrokerId);
        Assert.Null(state.RefreshHandle);
    }

    [Fact]
    public async Task InitiateLoginAsync_ThrowsOnInvalidCredentials()
    {
        var adapter = CreateHandlerAdapter(_ => Ok(new
        {
            status = "error",
            message = "Invalid username or password (YYYY)",
            error_type = "MiraeException",
            data = (object?)null,
        }));

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.InitiateLoginAsync("bad", "bad", CancellationToken.None));

        Assert.Equal(BrokerErrorKind.BrokerRejected, ex.Kind);
    }

    [Fact]
    public async Task CompleteLoginWithOtpAsync_ReturnsSession()
    {
        var loginTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var adapter = CreateHandlerAdapter(_ => Ok(new
        {
            status = "success",
            data = new
            {
                user_id = "538",
                user_name = "TESTUSER",
                broker = "MIRAE",
                api_key = ApiKey,
                access_token = "jwt-token-here",
                public_token = "pub-uuid",
                login_time = loginTime,
                exchanges = new[] { "NSE", "NFO", "CDS" },
                products = new[] { "CNC", "NRML", "MIS" },
                order_types = new[] { "MARKET", "LIMIT" },
            },
        }));

        var state = new BrokerLoginState { BrokerId = BrokerId.MStock, Username = "myuser" };
        var session = await adapter.CompleteLoginWithOtpAsync(state, "123456", CancellationToken.None);

        Assert.Equal(BrokerId.MStock, session.BrokerId);
        Assert.Equal("538", session.UserId);
        Assert.Equal("jwt-token-here", session.AccessToken);
        Assert.Contains("NSE", session.Exchanges);
        Assert.False(session.IsExpired);
    }

    [Fact]
    public async Task CompleteLoginWithOtpAsync_ThrowsOnInvalidOtp()
    {
        var adapter = CreateHandlerAdapter(_ => Ok(new
        {
            status = "error",
            message = "The entered OTP is incorrect. Please proceed to login page. (-MACM60)",
            error_type = "MiraeException",
            data = (object?)null,
        }));

        var state = new BrokerLoginState { BrokerId = BrokerId.MStock, Username = "myuser" };
        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.CompleteLoginWithOtpAsync(state, "000000", CancellationToken.None));

        Assert.Equal(BrokerErrorKind.BrokerRejected, ex.Kind);
        Assert.Contains("OTP", ex.Message);
    }

    [Fact]
    public async Task CompleteLoginWithTotpAsync_ReturnsSession()
    {
        var adapter = CreateHandlerAdapter(_ => Ok(new
        {
            status = "success",
            data = new
            {
                user_id = "538",
                access_token = "jwt-from-totp",
                login_time = "2024-09-26 03:34:48",
                exchanges = Array.Empty<string>(),
                products = Array.Empty<string>(),
                order_types = Array.Empty<string>(),
            },
        }));

        var state = new BrokerLoginState { BrokerId = BrokerId.MStock, Username = "myuser" };
        var session = await adapter.CompleteLoginWithTotpAsync(state, "654321", CancellationToken.None);

        Assert.Equal("jwt-from-totp", session.AccessToken);
    }

    [Fact]
    public async Task CompleteLoginWithTotpAsync_ThrowsOnInvalidTotp()
    {
        var adapter = CreateHandlerAdapter(_ => BadRequest(new
        {
            status = "error",
            message = "Please enter correct TOTP",
            error_type = "MiraeException",
            data = (object?)null,
        }));

        var state = new BrokerLoginState { BrokerId = BrokerId.MStock, Username = "myuser" };
        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.CompleteLoginWithTotpAsync(state, "000000", CancellationToken.None));

        Assert.Equal(BrokerErrorKind.Unknown, ex.Kind);
    }

    [Fact]
    public async Task GetFundsAsync_MapsFundSummary()
    {
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new
        {
            status = "success",
            data = new[]
            {
                new
                {
                    AVAILABLE_BALANCE = "299972678840.29",
                    AMOUNT_UTILIZED = "27395824.71",
                    CLEAR_BALANCE = "199999949998",
                    COLLATERALS = "74668",
                },
            },
        }));

        var funds = await adapter.GetFundsAsync(CancellationToken.None);

        Assert.Equal(299972678840.29m, funds.AvailableBalance.Amount);
        Assert.Equal(27395824.71m, funds.UtilizedAmount.Amount);
        Assert.Equal(199999949998m, funds.ClearBalance.Amount);
        Assert.Equal(74668m, funds.Collateral.Amount);
    }

    [Fact]
    public async Task GetOrderBookAsync_MapsOrders()
    {
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new
        {
            status = "success",
            data = new[]
            {
                new
                {
                    placed_by = "538",
                    order_id = "1151240930103",
                    exchange = "NSE",
                    tradingsymbol = "INFY",
                    instrument_token = 1594,
                    order_type = "LIMIT",
                    transaction_type = "BUY",
                    validity = "DAY",
                    product = "INTRADAY",
                    quantity = 10L,
                    disclosed_quantity = 0L,
                    price = 1250m,
                    trigger_price = 0m,
                    average_price = 0m,
                    filled_quantity = 0L,
                    pending_quantity = 0L,
                    cancelled_quantity = 0L,
                    status = "Rejected",
                    status_message = "FUND LIMIT INSUFFICIENT",
                    order_timestamp = "30-09-2024 15:45:46",
                    variety = (string?)null,
                },
            },
        }));

        var orders = await adapter.GetOrderBookAsync(CancellationToken.None);

        Assert.Single(orders);
        var order = orders[0];
        Assert.Equal("1151240930103", order.BrokerOrderId);
        Assert.Equal("INFY", order.TradingSymbol);
        Assert.Equal("NSE", order.Exchange);
        Assert.Equal("BUY", order.TransactionType);
        Assert.Equal("LIMIT", order.OrderType);
        Assert.Equal(10, order.Quantity);
        Assert.Equal("Rejected", order.Status);
        Assert.NotNull(order.OrderTimestamp);
    }

    [Fact]
    public async Task GetTradeBookAsync_MapsTradeBookEntries()
    {
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new
        {
            status = "success",
            data = new[]
            {
                new
                {
                    ALGO_ID = "0",
                    BUY_SELL = "Sell",
                    CLIENT_ID = "MA68XXXXX",
                    EXCHANGE = "NSE",
                    EXCH_ORDER_NUMBER = "1100000048872942",
                    FULL_SYMBOL = "VODAFONE IDEA LIMITED",
                    INSTRUMENT_NAME = "EQUITY",
                    ORDER_DATE_TIME = "10-06-2025 13:08:42",
                    ORDER_NUMBER = "21612506101476",
                    ORDER_TYPE = "MARKET",
                    PRICE = 6.98m,
                    PRODUCT = "CNC",
                    QUANTITY = 4L,
                    SYMBOL = "IDEA",
                    TRADE_NUMBER = "206465040",
                    TRADE_VALUE = 27.92m,
                },
            },
        }));

        var trades = await adapter.GetTradeBookAsync(CancellationToken.None);

        Assert.Single(trades);
        var trade = trades[0];
        Assert.Equal("206465040", trade.TradeId);
        Assert.Equal("IDEA", trade.TradingSymbol);
        Assert.Equal("Sell", trade.TransactionType);
        Assert.Equal(4, trade.Quantity);
        Assert.Equal(6.98m, trade.AveragePrice);
    }

    [Fact]
    public async Task GetPositionsAsync_MapsNetPositions()
    {
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new
        {
            status = "success",
            data = new
            {
                net = new[]
                {
                    new
                    {
                        tradingsymbol = "YESBANK",
                        exchange = "NSE",
                        instrument_token = 11915L,
                        product = "",
                        quantity = 100L,
                        overnight_quantity = 0L,
                        average_price = 19.05m,
                        close_price = 27.65m,
                        last_price = 27.65m,
                        pnl = 0m,
                        m2m = 860m,
                        buy_quantity = 100L,
                        buy_price = 19.05m,
                        buy_value = 1905m,
                        sell_quantity = 0L,
                        sell_price = 0m,
                        sell_value = 0m,
                    },
                },
                day = (object?)null,
            },
        }));

        var positions = await adapter.GetPositionsAsync(CancellationToken.None);

        Assert.Single(positions);
        var pos = positions[0];
        Assert.Equal("YESBANK", pos.TradingSymbol);
        Assert.Equal(100, pos.Quantity);
        Assert.Equal(860m, pos.MarkToMarket);
    }

    [Fact]
    public async Task GetHoldingsAsync_MapsHoldings()
    {
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new
        {
            status = "success",
            data = new[]
            {
                new
                {
                    tradingsymbol = "BANK OF MAHARASHTRA",
                    instrument_token = 11377L,
                    isin = "INE457A01014",
                    quantity = 10L,
                    used_quantity = 1L,
                    t1_quantity = 0L,
                    average_price = 30m,
                    last_price = 84.7m,
                    close_price = 51.1m,
                    pnl = 0m,
                    day_change = 0m,
                    day_change_percentage = 0m,
                },
            },
        }));

        var holdings = await adapter.GetHoldingsAsync(CancellationToken.None);

        Assert.Single(holdings);
        var h = holdings[0];
        Assert.Equal("BANK OF MAHARASHTRA", h.TradingSymbol);
        Assert.Equal("INE457A01014", h.Isin);
        Assert.Equal(10, h.Quantity);
        Assert.Equal(30m, h.AveragePrice);
    }

    [Fact]
    public async Task GetLtpQuotesAsync_MapsLtpData()
    {
        var data = new Dictionary<string, object>
        {
            ["NSE:ACC-EQ"] = new { instrument_token = 22L, last_price = 1373.3m },
            ["BSE:ACC-A"] = new { instrument_token = 500410L, last_price = 1374.6m },
        };
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new { status = "success", data }));

        var quotes = await adapter.GetLtpQuotesAsync(["NSE:ACC-EQ", "BSE:ACC-A"], CancellationToken.None);

        Assert.Equal(2, quotes.Count);
        var nseQuote = quotes.Single(q => q.Exchange == "NSE");
        Assert.Equal("ACC-EQ", nseQuote.TradingSymbol);
        Assert.Equal(1373.3m, nseQuote.LastPrice);
    }

    [Fact]
    public async Task GetOhlcQuotesAsync_MapsOhlcData()
    {
        var data = new Dictionary<string, object>
        {
            ["NSE:ACC-EQ"] = new
            {
                instrument_token = 22L,
                last_price = 1373.3m,
                ohlc = new { open = 1390m, high = 1393.2m, low = 1370.2m, close = 1383.2m },
            },
        };
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new { status = "success", data }));

        var quotes = await adapter.GetOhlcQuotesAsync(["NSE:ACC-EQ"], CancellationToken.None);

        Assert.Single(quotes);
        var q = quotes[0];
        Assert.Equal(1390m, q.Open);
        Assert.Equal(1393.2m, q.High);
        Assert.Equal(1370.2m, q.Low);
        Assert.Equal(1383.2m, q.Close);
    }

    [Fact]
    public async Task GetInstrumentMasterAsync_ParsesCsv()
    {
        var csv = "instrument_token,exchange_token,tradingsymbol,name,last_price,expiry,strike,tick_size,lot_size,instrument_type,segment,exchange\n" +
                  "22,22,ACC,ABB INDIA LIMITED,,,,0.5,1,EQ,EQ,NSE\n" +
                  "1594,1594,INFY,INFOSYS LTD,,,,0.05,1,EQ,EQ,NSE\n";

        var adapter = CreateAuthenticatedAdapter(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(csv),
        });

        var instruments = await adapter.GetInstrumentMasterAsync(CancellationToken.None);

        Assert.Equal(2, instruments.Count);
        var acc = instruments.Single(i => i.TradingSymbol == "ACC");
        Assert.Equal("NSE", acc.Exchange);
        Assert.Equal(22, acc.BrokerInstrumentToken);
        Assert.Equal(0.5m, acc.TickSize);
        Assert.Equal(BrokerId.MStock, acc.BrokerId);
    }

    [Fact]
    public async Task ReadOperation_ThrowsAuthExpired_WhenNoSession()
    {
        var adapter = CreateHandlerAdapter(_ => Ok(new { status = "success", data = Array.Empty<object>() }));

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.GetFundsAsync(CancellationToken.None));

        Assert.Equal(BrokerErrorKind.AuthExpired, ex.Kind);
    }

    [Fact]
    public async Task ReadOperation_ThrowsAuthExpired_On401()
    {
        var adapter = CreateAuthenticatedAdapter(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                status = "error",
                message = "Invalid request. Please try again.",
                error_type = "TokenException",
                data = (object?)null,
            })),
        });

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.GetFundsAsync(CancellationToken.None));

        Assert.Equal(BrokerErrorKind.AuthExpired, ex.Kind);
    }

    [Fact]
    public async Task ReadOperation_ThrowsAuthExpired_On403_ApiKeyExpired()
    {
        var adapter = CreateAuthenticatedAdapter(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                status = "error",
                message = "API is suspended/expired for use.",
                error_type = "APIKeyException",
                data = (object?)null,
            })),
        });

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.GetFundsAsync(CancellationToken.None));

        Assert.Equal(BrokerErrorKind.AuthExpired, ex.Kind);
    }

    [Fact]
    public async Task ReadOperation_ThrowsRateLimited_On429()
    {
        var adapter = CreateAuthenticatedAdapter(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{}"),
        });

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.GetFundsAsync(CancellationToken.None));

        Assert.Equal(BrokerErrorKind.RateLimited, ex.Kind);
    }

    [Fact]
    public async Task ReadOperation_ThrowsBrokerUnavailable_On503()
    {
        var adapter = CreateAuthenticatedAdapter(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{}"),
        });

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.GetFundsAsync(CancellationToken.None));

        Assert.Equal(BrokerErrorKind.BrokerUnavailable, ex.Kind);
    }

    [Fact]
    public async Task ReadOperation_ThrowsBusinessError_OnStatusErrorIn200()
    {
        // m.Stock returns HTTP 200 with status:"error" for business errors.
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new
        {
            status = "error",
            message = "System is not connected to NSE Equity market",
            error_type = "InputException",
            data = (object?)null,
        }));

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.GetOrderBookAsync(CancellationToken.None));

        Assert.Equal(BrokerErrorKind.MarketClosed, ex.Kind);
    }

    [Fact]
    public async Task LogoutAsync_ClearsSession()
    {
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new { status = "success", data = "Success" }));

        await adapter.LogoutAsync(CancellationToken.None);

        // After logout, any call should fail with AuthExpired
        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.GetFundsAsync(CancellationToken.None));
        Assert.Equal(BrokerErrorKind.AuthExpired, ex.Kind);
    }

    [Fact]
    public async Task GetLtpQuotesAsync_EmptyInput_ReturnsEmpty()
    {
        var adapter = CreateAuthenticatedAdapter(_ => Ok(new { status = "success", data = new { } }));
        var quotes = await adapter.GetLtpQuotesAsync([], CancellationToken.None);
        Assert.Empty(quotes);
    }

    // --- Type B auth (https://tradingapi.mstock.com/docs/v1/typeB/User/) ---

    private const string TypeBRefreshHandle = "697c39bf-9411-46b0-81c2-67448ee99c72";
    private const string TypeBJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature";

    private static MStockAdapter CreateTypeBAdapter(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => CreateAdapter(handler, MStockApiType.TypeB);

    [Fact]
    public async Task TypeB_InitiateLogin_SendsJsonClientCodeAndCapturesRefreshHandle()
    {
        string? capturedBody = null;
        string? capturedContentType = null;
        var adapter = CreateTypeBAdapter(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().Result;
            capturedContentType = req.Content?.Headers.ContentType?.MediaType;
            return Ok(new
            {
                status = "true",
                message = "Please enter the OTP",
                data = new { jwtToken = TypeBRefreshHandle, refreshToken = "", feedToken = "", state = "live" },
            });
        });

        var state = await adapter.InitiateLoginAsync("myuser", "mypass", CancellationToken.None);

        Assert.NotNull(capturedBody);
        Assert.Equal("application/json", capturedContentType);
        Assert.Contains("\"clientcode\":\"myuser\"", capturedBody);
        Assert.Contains("\"password\":\"mypass\"", capturedBody);
        Assert.Equal("myuser", state.Username);
        Assert.Equal(TypeBRefreshHandle, state.RefreshHandle);
    }

    [Fact]
    public async Task TypeB_InitiateLogin_ThrowsOnStatusFalse()
    {
        var adapter = CreateTypeBAdapter(_ => Ok(new
        {
            status = "false",
            message = "Invalid username or password. 9 attempts remaining",
            errorcode = "MA500",
            data = (object?)null,
        }));

        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.InitiateLoginAsync("bad", "bad", CancellationToken.None));

        Assert.Contains("Invalid username or password", ex.Message);
    }

    [Fact]
    public async Task TypeB_CompleteLoginWithOtp_SendsRefreshTokenAndOtp_ReturnsSession()
    {
        string? capturedBody = null;
        string? capturedPrivateKey = null;
        var adapter = CreateTypeBAdapter(req =>
        {
            // First call: connect/login. Second call: session/token.
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/connect/login", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    status = "true",
                    data = new { jwtToken = TypeBRefreshHandle, refreshToken = "", feedToken = "", state = "live" },
                });
            }

            capturedBody = req.Content?.ReadAsStringAsync().Result;
            capturedPrivateKey = req.Headers.TryGetValues("X-PrivateKey", out var pk) ? pk.FirstOrDefault() : null;
            return Ok(new
            {
                status = "true",
                data = new
                {
                    jwtToken = TypeBJwt,
                    refreshToken = TypeBRefreshHandle,
                    feedToken = "feed",
                    ClientName = "RAHUL",
                    ClientId = "MA68XXXXX",
                    exchanges = new[] { "NSE", "NFO" },
                },
            });
        });

        var state = await adapter.InitiateLoginAsync("myuser", "mypass", CancellationToken.None);
        var session = await adapter.CompleteLoginWithOtpAsync(state, "123456", CancellationToken.None);

        Assert.Equal(BrokerId.MStock, session.BrokerId);
        Assert.Equal(TypeBJwt, session.AccessToken);
        Assert.Equal("MA68XXXXX", session.UserId);
        Assert.Contains("NSE", session.Exchanges);
        Assert.False(session.IsExpired);
        Assert.NotNull(capturedBody);
        Assert.Contains($"\"refreshToken\":\"{TypeBRefreshHandle}\"", capturedBody);
        Assert.Contains("\"otp\":\"123456\"", capturedBody);
        Assert.Equal(ApiKey, capturedPrivateKey);
    }

    [Fact]
    public async Task TypeB_CompleteLoginWithTotp_SendsRefreshTokenAndTotp_ReturnsSession()
    {
        string? capturedBody = null;
        var adapter = CreateTypeBAdapter(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/connect/login", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    status = "true",
                    data = new { jwtToken = TypeBRefreshHandle, refreshToken = "", feedToken = "", state = "live" },
                });
            }

            capturedBody = req.Content?.ReadAsStringAsync().Result;
            // verifytotp success response uses a boolean status (normalized by the converter).
            return Ok(new
            {
                status = true,
                data = new
                {
                    ClientName = "RAHUL",
                    ClientId = "MA68XXXXX",
                    exchanges = new[] { "NSE" },
                    jwtToken = TypeBJwt,
                    refreshToken = TypeBRefreshHandle,
                    feedToken = "feed",
                },
            });
        });

        var state = await adapter.InitiateLoginAsync("myuser", "mypass", CancellationToken.None);
        var session = await adapter.CompleteLoginWithTotpAsync(state, "654321", CancellationToken.None);

        Assert.Equal(TypeBJwt, session.AccessToken);
        Assert.NotNull(capturedBody);
        Assert.Contains($"\"refreshToken\":\"{TypeBRefreshHandle}\"", capturedBody);
        Assert.Contains("\"totp\":\"654321\"", capturedBody);
    }

    [Fact]
    public async Task TypeB_CompleteLoginWithOtp_ThrowsOnExpiredOtp()
    {
        var adapter = CreateTypeBAdapter(_ => Ok(new
        {
            status = "false",
            message = "Entered OTP has been expired. Please regenerate a new one & enter the same.",
            errorcode = "MA500",
            data = (object?)null,
        }));

        var state = new BrokerLoginState
        {
            BrokerId = BrokerId.MStock,
            Username = "myuser",
            RefreshHandle = TypeBRefreshHandle,
        };
        var ex = await Assert.ThrowsAsync<BrokerException>(
            () => adapter.CompleteLoginWithOtpAsync(state, "000000", CancellationToken.None));

        Assert.Contains("OTP", ex.Message);
    }

    [Fact]
    public async Task TypeB_AuthenticatedRequest_UsesBearerAndPrivateKeyHeaders()
    {
        string? capturedAuthScheme = null;
        string? capturedAuthParam = null;
        string? capturedPrivateKey = null;
        var adapter = CreateTypeBAdapter(req =>
        {
            capturedAuthScheme = req.Headers.Authorization?.Scheme;
            capturedAuthParam = req.Headers.Authorization?.Parameter;
            capturedPrivateKey = req.Headers.TryGetValues("X-PrivateKey", out var pk) ? pk.FirstOrDefault() : null;
            return Ok(new { status = "success", data = Array.Empty<object>() });
        });

        adapter.SetSession(new BrokerSession
        {
            BrokerId = BrokerId.MStock,
            AccessToken = TypeBJwt,
            UserId = "MA68XXXXX",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(10),
        });

        await adapter.GetFundsAsync(CancellationToken.None);

        Assert.Equal("Bearer", capturedAuthScheme);
        Assert.Equal(TypeBJwt, capturedAuthParam);
        Assert.Equal(ApiKey, capturedPrivateKey);
    }

    // --- Helpers ---

    private static MStockAdapter CreateHandlerAdapter(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => CreateAdapter(handler);

    private static MStockAdapter CreateAuthenticatedAdapter(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var adapter = CreateAdapter(handler);
        SetSession(adapter);
        return adapter;
    }

    private static HttpResponseMessage Ok(object body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body)),
        };

    private static HttpResponseMessage BadRequest(object body) =>
        new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(JsonSerializer.Serialize(body)),
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
