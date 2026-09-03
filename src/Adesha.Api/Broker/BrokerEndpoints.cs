using System.Security.Claims;
using Adesha.Application.Brokers;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.Abstractions.Models;
using FluentValidation;

namespace Adesha.Api.Broker;

public static class BrokerEndpoints
{
    public static IEndpointRouteBuilder MapBrokerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/broker");

        group.MapGet("/capabilities", GetCapabilitiesAsync);
        group.MapGet("/session", GetSessionAsync).RequireAuthorization();
        group.MapPost("/login/initiate", InitiateLoginAsync).RequireAuthorization();
        group.MapPost("/login/complete-otp", CompleteLoginWithOtpAsync).RequireAuthorization();
        group.MapPost("/login/complete-totp", CompleteLoginWithTotpAsync).RequireAuthorization();
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> GetCapabilitiesAsync(
        IEnumerable<IBrokerAdapter> adapters,
        CancellationToken cancellationToken)
    {
        // Serialize BrokerId as a string so the Angular client can filter by name.
        var caps = adapters.Select(a => new
        {
            brokerId = Enum.GetName(typeof(BrokerId), a.Capabilities.BrokerId),
            a.Capabilities.DisplayName,
            a.Capabilities.SupportsOtpLogin,
            a.Capabilities.SupportsTotpLogin,
            a.Capabilities.SupportsInstrumentMaster,
            a.Capabilities.SupportsLtpQuotes,
            a.Capabilities.SupportsOhlcQuotes,
            a.Capabilities.SupportsOrderBook,
            a.Capabilities.SupportsTradeBook,
            a.Capabilities.SupportsPositions,
            a.Capabilities.SupportsHoldings,
            a.Capabilities.SupportsFunds,
            a.Capabilities.SupportsOrderPlacement,
            a.Capabilities.SupportsOrderModification,
            a.Capabilities.SupportsOrderCancellation,
            a.Capabilities.SupportedExchanges,
            a.Capabilities.SupportedProducts,
            a.Capabilities.SupportedOrderTypes,
        });
        return Results.Ok(caps);
    }

    private static async Task<IResult> GetSessionAsync(
        BrokerId brokerId,
        IBrokerSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(brokerId, cancellationToken);
        if (session is null)
        {
            return Results.Ok(new { isLoggedIn = false });
        }

        return Results.Ok(new
        {
            isLoggedIn = !session.IsExpired,
            brokerId = session.BrokerId.ToString(),
            session.UserId,
            session.ExpiresAtUtc,
            session.Exchanges,
            session.Products,
            session.OrderTypes,
        });
    }

    private static async Task<IResult> InitiateLoginAsync(
        BrokerInitiateLoginRequest request,
        IValidator<BrokerInitiateLoginRequest> validator,
        IEnumerable<IBrokerAdapter> adapters,
        IBrokerLoginStateStore loginStateStore,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!TryParseBrokerId(request.BrokerId, out var brokerId))
        {
            return Results.BadRequest(new { error = $"Unknown broker '{request.BrokerId}'." });
        }

        var adapter = adapters.FirstOrDefault(a => a.BrokerId == brokerId);
        if (adapter is null)
        {
            return Results.BadRequest(new { error = $"Broker '{request.BrokerId}' is not configured." });
        }

        if (!adapter.Capabilities.SupportsOtpLogin)
        {
            return Results.BadRequest(new { error = $"Broker '{request.BrokerId}' does not support credential-based login." });
        }

        var state = await adapter.InitiateLoginAsync(request.Username, request.Password, cancellationToken);
        var userId = GetUserId(user);
        await loginStateStore.SaveAsync(userId, state, cancellationToken);

        return Results.Ok(new { message = "OTP sent to the registered mobile number." });
    }

    private static async Task<IResult> CompleteLoginWithOtpAsync(
        BrokerCompleteOtpRequest request,
        IValidator<BrokerCompleteOtpRequest> validator,
        IEnumerable<IBrokerAdapter> adapters,
        IBrokerLoginStateStore loginStateStore,
        IBrokerSessionStore sessionStore,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!TryParseBrokerId(request.BrokerId, out var brokerId))
        {
            return Results.BadRequest(new { error = $"Unknown broker '{request.BrokerId}'." });
        }

        var userId = GetUserId(user);
        var state = await loginStateStore.PopAsync(userId, brokerId, cancellationToken);
        if (state is null)
        {
            return Results.BadRequest(new { error = "Login session expired or was not initiated. Start again." });
        }

        var adapter = adapters.FirstOrDefault(a => a.BrokerId == brokerId);
        if (adapter is null)
        {
            return Results.BadRequest(new { error = $"Broker '{request.BrokerId}' is not configured." });
        }

        var session = await adapter.CompleteLoginWithOtpAsync(state, request.Otp, cancellationToken);
        await sessionStore.SaveSessionAsync(session, cancellationToken);

        return Results.Ok(MapSession(session));
    }

    private static async Task<IResult> CompleteLoginWithTotpAsync(
        BrokerCompleteTotpRequest request,
        IValidator<BrokerCompleteTotpRequest> validator,
        IEnumerable<IBrokerAdapter> adapters,
        IBrokerLoginStateStore loginStateStore,
        IBrokerSessionStore sessionStore,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!TryParseBrokerId(request.BrokerId, out var brokerId))
        {
            return Results.BadRequest(new { error = $"Unknown broker '{request.BrokerId}'." });
        }

        var userId = GetUserId(user);
        var state = await loginStateStore.PopAsync(userId, brokerId, cancellationToken);
        if (state is null)
        {
            return Results.BadRequest(new { error = "Login session expired or was not initiated. Start again." });
        }

        var adapter = adapters.FirstOrDefault(a => a.BrokerId == brokerId);
        if (adapter is null)
        {
            return Results.BadRequest(new { error = $"Broker '{request.BrokerId}' is not configured." });
        }

        var session = await adapter.CompleteLoginWithTotpAsync(state, request.Totp, cancellationToken);
        await sessionStore.SaveSessionAsync(session, cancellationToken);

        return Results.Ok(MapSession(session));
    }

    private static async Task<IResult> LogoutAsync(
        BrokerLogoutRequest request,
        IValidator<BrokerLogoutRequest> validator,
        IEnumerable<IBrokerAdapter> adapters,
        IBrokerSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!TryParseBrokerId(request.BrokerId, out var brokerId))
        {
            return Results.BadRequest(new { error = $"Unknown broker '{request.BrokerId}'." });
        }

        var adapter = adapters.FirstOrDefault(a => a.BrokerId == brokerId);
        if (adapter is null)
        {
            return Results.BadRequest(new { error = $"Broker '{request.BrokerId}' is not configured." });
        }

        var session = await sessionStore.GetSessionAsync(brokerId, cancellationToken);
        if (session is not null && !session.IsExpired)
        {
            adapter.SetSession(session);
            await adapter.LogoutAsync(cancellationToken);
        }

        await sessionStore.ClearSessionAsync(brokerId, cancellationToken);
        return Results.NoContent();
    }

    private static bool TryParseBrokerId(string value, out BrokerId brokerId)
    {
        return Enum.TryParse(value, ignoreCase: true, out brokerId)
            && Enum.IsDefined(brokerId);
    }

    private static string GetUserId(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User identity is missing.");
    }

    private static object MapSession(BrokerSession session)
    {
        return new
        {
            brokerId = session.BrokerId.ToString(),
            session.UserId,
            session.ExpiresAtUtc,
            session.Exchanges,
            session.Products,
            session.OrderTypes,
        };
    }
}
