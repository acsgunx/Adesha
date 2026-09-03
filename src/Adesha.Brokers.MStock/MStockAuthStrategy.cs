using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Errors;
using Adesha.Brokers.Abstractions.Models;
using Adesha.Brokers.MStock.Dtos;

namespace Adesha.Brokers.MStock;

/// <summary>
/// Encapsulates the differences between the m.Stock TypeA and TypeB auth surfaces:
/// URL path segment, request encoding (form vs JSON), login/session request bodies,
/// the auth header format, and response parsing. The adapter delegates all auth
/// shaping here so the read-path code stays shared.
/// </summary>
internal interface IMStockAuthStrategy
{
    string PathSegment { get; }

    /// <summary>Builds the <c>connect/login</c> request (step 1: triggers OTP).</summary>
    HttpRequestMessage BuildLoginRequest(string username, string password);

    /// <summary>
    /// Parses the <c>connect/login</c> response, throwing on business errors. Returns
    /// the refresh handle required by the next step (TypeB only; TypeA returns null).
    /// </summary>
    string? ProcessLoginResponse(string body);

    /// <summary>Builds the <c>session/token</c> request (step 2: OTP -> session).</summary>
    HttpRequestMessage BuildSessionTokenRequest(string apiKey, string otp, string? refreshToken);

    /// <summary>Builds the <c>session/verifytotp</c> request (TOTP -> session).</summary>
    HttpRequestMessage BuildVerifyTotpRequest(string apiKey, string totp, string? refreshToken);

    /// <summary>Parses a session response, throwing on business errors.</summary>
    BrokerSession ParseSessionResponse(string body);

    /// <summary>Applies the auth header(s) to an authenticated request.</summary>
    void ApplyAuth(HttpRequestMessage request, string apiKey, string accessToken);
}

/// <summary>
/// TypeA: form-urlencoded, <c>Authorization: token api_key:jwtToken</c>.
/// Source: https://tradingapi.mstock.com/docs/v1/typeA/User/
/// </summary>
internal sealed class MStockTypeAAuthStrategy : IMStockAuthStrategy
{
    private const string MiraeVersion = "1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string PathSegment => "typea";

    public HttpRequestMessage BuildLoginRequest(string username, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "connect/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password,
            }),
        };
        request.Headers.Add("X-Mirae-Version", MiraeVersion);
        return request;
    }

    public string? ProcessLoginResponse(string body)
    {
        var loginResult = JsonSerializer.Deserialize<MStockResponse<MStockLoginData>>(body, JsonOptions);
        // TypeA docs: success = "success" + data with is_error:"false".
        // Failures use "error" (or HTTP 4xx/5xx handled at the HTTP layer).
        if (loginResult?.Status != "success" || loginResult?.Data is null)
        {
            throw MStockErrorMapper.MapBusinessError(loginResult ?? new MStockResponse<MStockLoginData>());
        }

        // TypeA login only triggers OTP; there is no refresh handle to carry forward.
        return null;
    }

    public HttpRequestMessage BuildSessionTokenRequest(string apiKey, string otp, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "session/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["api_key"] = apiKey,
                ["request_token"] = otp,
                ["checksum"] = "L",
            }),
        };
        request.Headers.Add("X-Mirae-Version", MiraeVersion);
        return request;
    }

    public HttpRequestMessage BuildVerifyTotpRequest(string apiKey, string totp, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "session/verifytotp")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["api_key"] = apiKey,
                ["totp"] = totp,
            }),
        };
        request.Headers.Add("X-Mirae-Version", MiraeVersion);
        return request;
    }

    public BrokerSession ParseSessionResponse(string body)
    {
        var sessionResult = JsonSerializer.Deserialize<MStockResponse<MStockSessionData>>(body, JsonOptions);
        if (sessionResult?.Status != "success" || sessionResult?.Data is null)
        {
            throw MStockErrorMapper.MapBusinessError(sessionResult ?? new MStockResponse<MStockSessionData>());
        }

        return BuildSession(sessionResult.Data);
    }

    public void ApplyAuth(HttpRequestMessage request, string apiKey, string accessToken)
    {
        request.Headers.Add("X-Mirae-Version", MiraeVersion);
        request.Headers.Authorization = new AuthenticationHeaderValue("token", $"{apiKey}:{accessToken}");
    }

    internal static BrokerSession BuildSession(MStockSessionData data)
    {
        // Token expires at midnight IST of the generation day, or 12 hours, whichever is first.
        // We use the conservative 12-hour window from the generation time.
        var loginTime = ParseLoginTime(data.LoginTime);
        var expiresAt = loginTime.AddHours(12);

        return new BrokerSession
        {
            BrokerId = BrokerId.MStock,
            AccessToken = data.AccessToken
                ?? throw new BrokerException(BrokerErrorKind.AuthFailed, "No access token in session response."),
            UserId = data.UserId ?? data.UserName ?? "unknown",
            ExpiresAtUtc = expiresAt,
            Exchanges = data.Exchanges ?? [],
            Products = data.Products ?? [],
            OrderTypes = data.OrderTypes ?? [],
        };
    }

    private static DateTimeOffset ParseLoginTime(string? loginTime)
    {
        // Docs show format "2024-09-26 03:34:48" (IST, no timezone).
        if (DateTimeOffset.TryParseExact(loginTime, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// TypeB: JSON bodies, <c>Authorization: Bearer jwtToken</c> + <c>X-PrivateKey: api_key</c>.
/// Login returns a refresh handle (in <c>jwtToken</c>) that is carried into
/// <c>session/token</c> (OTP) or <c>session/verifytotp</c> (TOTP).
/// Source: https://tradingapi.mstock.com/docs/v1/typeB/User/
/// </summary>
internal sealed class MStockTypeBAuthStrategy : IMStockAuthStrategy
{
    private const string MiraeVersion = "1";
    private const string JsonContentType = "application/json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string PathSegment => "typeb";

    public HttpRequestMessage BuildLoginRequest(string username, string password)
    {
        // TypeB login takes clientcode/password/totp/state as a JSON body.
        var body = JsonSerializer.Serialize(new
        {
            clientcode = username,
            password = password,
            totp = string.Empty,
            state = string.Empty,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "connect/login")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, JsonContentType),
        };
        request.Headers.Add("X-Mirae-Version", MiraeVersion);
        return request;
    }

    public string? ProcessLoginResponse(string body)
    {
        var loginResult = JsonSerializer.Deserialize<MStockResponse<MStockTypeBLoginData>>(body, JsonOptions);
        // TypeB success status is "true" (or boolean true, normalized by the converter);
        // anything else ("false"/"error") is a failure.
        if (!string.Equals(loginResult?.Status, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw MStockErrorMapper.MapBusinessError(loginResult ?? new MStockResponse<MStockTypeBLoginData>());
        }

        // The refresh handle for the next step is the login response's jwtToken. The
        // docs' example shows refreshToken as an empty string while jwtToken holds the
        // UUID-like handle, so prefer a non-empty refreshToken and fall back to jwtToken.
        var data = loginResult!.Data;
        return !string.IsNullOrEmpty(data?.RefreshToken) ? data.RefreshToken : data?.JwtToken;
    }

    public HttpRequestMessage BuildSessionTokenRequest(string apiKey, string otp, string? refreshToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            refreshToken = refreshToken ?? string.Empty,
            otp = otp,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "session/token")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, JsonContentType),
        };
        AddHeaders(request, apiKey);
        return request;
    }

    public HttpRequestMessage BuildVerifyTotpRequest(string apiKey, string totp, string? refreshToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            refreshToken = refreshToken ?? string.Empty,
            totp = totp,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "session/verifytotp")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, JsonContentType),
        };
        AddHeaders(request, apiKey);
        return request;
    }

    public BrokerSession ParseSessionResponse(string body)
    {
        var sessionResult = JsonSerializer.Deserialize<MStockResponse<MStockTypeBSessionData>>(body, JsonOptions);
        if (!string.Equals(sessionResult?.Status, "true", StringComparison.OrdinalIgnoreCase)
            || sessionResult?.Data is null)
        {
            throw MStockErrorMapper.MapBusinessError(sessionResult ?? new MStockResponse<MStockTypeBSessionData>());
        }

        var data = sessionResult.Data;
        return new BrokerSession
        {
            BrokerId = BrokerId.MStock,
            AccessToken = data.JwtToken
                ?? throw new BrokerException(BrokerErrorKind.AuthFailed, "No access token in session response."),
            // TypeB does not return a login timestamp; use the conservative 12-hour window.
            UserId = data.ClientId ?? data.ClientName ?? "unknown",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(12),
            Exchanges = data.Exchanges ?? [],
        };
    }

    public void ApplyAuth(HttpRequestMessage request, string apiKey, string accessToken)
    {
        request.Headers.Add("X-Mirae-Version", MiraeVersion);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-PrivateKey", apiKey);
    }

    private static void AddHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("X-Mirae-Version", MiraeVersion);
        request.Headers.Add("X-PrivateKey", apiKey);
    }
}
