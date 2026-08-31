using Adesha.Api.Middleware;
using Adesha.Application.Auditing;
using Adesha.Domain.Auditing;
using Adesha.Infrastructure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Adesha.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");

        group.MapPost("/setup", SetupOwnerAsync);
        group.MapPost("/setup/confirm-totp", ConfirmTotpAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", (System.Security.Claims.ClaimsPrincipal user) =>
                Results.Ok(new
                {
                    username = user.FindFirst("name")?.Value
                        ?? user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                        ?? user.Identity?.Name,
                }))
            .RequireAuthorization();

        return routes;
    }

    /// <summary>Creates the single owner account. Only permitted while no account exists.</summary>
    private static async Task<IResult> SetupOwnerAsync(
        SetupOwnerRequest request,
        IValidator<SetupOwnerRequest> validator,
        UserManager<AdeshaUser> userManager,
        IAuditWriter auditWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var existing = await userManager.Users.FirstOrDefaultAsync(cancellationToken);
        AdeshaUser user;
        if (existing is not null)
        {
            // Setup abandoned before TOTP confirmation leaves an unusable account. Re-running
            // setup with the same credentials re-issues the authenticator key instead of
            // stranding the owner; anything else is a duplicate-owner attempt.
            if (existing.TwoFactorEnabled
                || existing.NormalizedUserName != userManager.NormalizeName(request.Username)
                || !await userManager.CheckPasswordAsync(existing, request.Password))
            {
                return Results.Conflict(new { error = "Owner account already exists." });
            }

            user = existing;
        }
        else
        {
            user = new AdeshaUser { UserName = request.Username };
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = [.. result.Errors.Select(e => e.Description)],
                });
            }
        }

        await userManager.ResetAuthenticatorKeyAsync(user);
        var sharedKey = await userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException("Authenticator key generation failed.");

        await auditWriter.AppendAsync(new AuditRecord
        {
            Actor = user.Id.ToString(),
            Action = existing is null ? "AppAccount.OwnerCreated" : "AppAccount.OwnerTotpKeyReissued",
            EntityType = nameof(AdeshaUser),
            EntityId = user.Id.ToString(),
            AfterState = $$"""{"username":"{{request.Username}}"}""",
            CorrelationId = httpContext.GetCorrelationId(),
        }, cancellationToken);

        var otpauthUri = $"otpauth://totp/Adesha:{Uri.EscapeDataString(request.Username)}?secret={sharedKey}&issuer=Adesha&digits=6";
        return Results.Ok(new SetupOwnerResponse { SharedKey = sharedKey, OtpauthUri = otpauthUri });
    }

    /// <summary>Confirms TOTP setup. The account cannot log in until this succeeds.</summary>
    private static async Task<IResult> ConfirmTotpAsync(
        ConfirmTotpRequest request,
        IValidator<ConfirmTotpRequest> validator,
        UserManager<AdeshaUser> userManager,
        IAuditWriter auditWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var user = await userManager.FindByNameAsync(request.Username);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Unauthorized();
        }

        var codeValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, request.TotpCode);
        if (!codeValid)
        {
            return Results.Unauthorized();
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);

        await auditWriter.AppendAsync(new AuditRecord
        {
            Actor = user.Id.ToString(),
            Action = "AppAccount.TotpEnabled",
            EntityType = nameof(AdeshaUser),
            EntityId = user.Id.ToString(),
            CorrelationId = httpContext.GetCorrelationId(),
        }, cancellationToken);

        return Results.Ok();
    }

    /// <summary>
    /// Password + mandatory TOTP in a single step. Failed TOTP counts toward lockout,
    /// so an attacker with the password cannot grind codes indefinitely.
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        UserManager<AdeshaUser> userManager,
        TokenService tokenService,
        IAuditWriter auditWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var user = await userManager.FindByNameAsync(request.Username);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Account is locked out.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return Results.Unauthorized();
        }

        if (!user.TwoFactorEnabled)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "TOTP setup is not complete.",
                detail: "Finish authenticator setup via /api/auth/setup/confirm-totp before logging in.");
        }

        var codeValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, request.TotpCode);
        if (!codeValid)
        {
            await userManager.AccessFailedAsync(user);
            return Results.Unauthorized();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var tokens = await tokenService.IssueTokenPairAsync(user, cancellationToken);

        await auditWriter.AppendAsync(new AuditRecord
        {
            Actor = user.Id.ToString(),
            Action = "AppAccount.LoginSucceeded",
            EntityType = nameof(AdeshaUser),
            EntityId = user.Id.ToString(),
            CorrelationId = httpContext.GetCorrelationId(),
        }, cancellationToken);

        return Results.Ok(tokens);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        IValidator<RefreshRequest> validator,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var tokens = await tokenService.RotateAsync(request.RefreshToken, cancellationToken);
        return tokens is null ? Results.Unauthorized() : Results.Ok(tokens);
    }

    private static async Task<IResult> LogoutAsync(
        RefreshRequest request,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
        await tokenService.RevokeAsync(request.RefreshToken, cancellationToken);
        return Results.NoContent();
    }
}
