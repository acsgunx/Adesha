using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Adesha.Api.Tests;

/// <summary>
/// End-to-end auth flow against real PostgreSQL and Redis (Testcontainers):
/// owner setup -> mandatory TOTP -> login -> JWT access -> refresh rotation ->
/// replay detection -> lockout.
/// </summary>
public sealed class AuthFlowTests(AdeshaApiFactory factory) : IClassFixture<AdeshaApiFactory>
{
    private const string Username = "owner";
    private const string Password = "Correct-Horse-Battery-Staple-42";

    private sealed record SetupResponse(string SharedKey, string OtpauthUri);
    private sealed record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Full_auth_lifecycle_works_and_enforces_all_rules()
    {
        var client = factory.CreateClient();

        // 1. Fresh system requires setup, and status reports TradingMode=Disabled (Rule 2 default).
        var status = await client.GetFromJsonAsync<JsonElement>("/api/system/status");
        Assert.Equal("Disabled", status.GetProperty("tradingMode").GetString());

        var setupRequired = await client.GetFromJsonAsync<JsonElement>("/api/system/setup-required");
        Assert.True(setupRequired.GetProperty("setupRequired").GetBoolean());

        // 2. Owner setup returns a TOTP shared key.
        var setupResponse = await client.PostAsJsonAsync("/api/auth/setup",
            new { username = Username, password = Password });
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        var setup = await setupResponse.Content.ReadFromJsonAsync<SetupResponse>(Json);
        Assert.NotNull(setup);
        Assert.StartsWith("otpauth://totp/Adesha:", setup.OtpauthUri);

        // 3. A second owner cannot be created (single-tenant).
        var duplicate = await client.PostAsJsonAsync("/api/auth/setup",
            new { username = "intruder", password = "another-long-password-99" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // 4. Login before TOTP confirmation is refused: the second factor is mandatory.
        var premature = await client.PostAsJsonAsync("/api/auth/login",
            new { username = Username, password = Password, totpCode = TotpGenerator.GenerateCode(setup.SharedKey) });
        if (premature.StatusCode != HttpStatusCode.Forbidden)
        {
            var body = await premature.Content.ReadAsStringAsync();
            Assert.Fail($"Premature login returned {premature.StatusCode}: {body}");
        }

        // 5. Confirm TOTP with a real RFC 6238 code.
        var confirm = await client.PostAsJsonAsync("/api/auth/setup/confirm-totp",
            new { username = Username, password = Password, totpCode = TotpGenerator.GenerateCode(setup.SharedKey) });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // 6. Wrong TOTP code is rejected.
        var badTotp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = Username, password = Password, totpCode = "000000" });
        Assert.Equal(HttpStatusCode.Unauthorized, badTotp.StatusCode);

        // 7. Correct password + TOTP issues a token pair.
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = Username, password = Password, totpCode = TotpGenerator.GenerateCode(setup.SharedKey) });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<TokenPair>(Json);
        Assert.NotNull(tokens);

        // 8. The access token authenticates protected endpoints; anonymous calls are rejected.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        using var authed = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        authed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = await client.SendAsync(authed);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        // 9. Refresh rotation: old refresh token stops working, new one is issued.
        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = await refreshed.Content.ReadFromJsonAsync<TokenPair>(Json);
        Assert.NotNull(rotated);
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);

        // 10. Replaying the rotated (revoked) token fails AND revokes the whole family.
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var familyRevoked = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, familyRevoked.StatusCode);

        // 11. Correlation id is echoed on responses.
        var correlated = await client.GetAsync("/api/system/status");
        Assert.True(correlated.Headers.Contains("X-Correlation-Id"));

        // 12. Repeated failures lock the account out (this test runs last: it locks the user).
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login",
                new { username = Username, password = "wrong-password-attempt-1", totpCode = "111111" });
        }

        var lockedOut = await client.PostAsJsonAsync("/api/auth/login",
            new { username = Username, password = Password, totpCode = "111111" });
        Assert.Equal(HttpStatusCode.Locked, lockedOut.StatusCode);
    }
}
