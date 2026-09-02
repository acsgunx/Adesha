using Adesha.Application.Configuration;
using Adesha.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Adesha.Api.SystemStatus;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system");

        // Anonymous: the Angular shell needs both before any login is possible.
        // In a single-tenant self-hosted app the trading mode is not sensitive.
        group.MapGet("/status", (IOptions<AdeshaOptions> options, IWebHostEnvironment env) =>
            Results.Ok(new
            {
                tradingMode = options.Value.TradingMode.ToString(),
                environment = env.EnvironmentName,
            }));

        // Setup is required until an owner account exists. TOTP is optional, so an account
        // without a confirmed authenticator can still log in with a password alone.
        group.MapGet("/setup-required", async (UserManager<AdeshaUser> userManager, CancellationToken ct) =>
            Results.Ok(new { setupRequired = !await userManager.Users.AnyAsync(ct) }));

        return routes;
    }
}
