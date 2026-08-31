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

        // Setup stays "required" until an owner has completed TOTP enrolment: an account
        // without a confirmed authenticator can never log in, so the shell must offer setup.
        group.MapGet("/setup-required", async (UserManager<AdeshaUser> userManager, CancellationToken ct) =>
            Results.Ok(new { setupRequired = !await userManager.Users.AnyAsync(u => u.TwoFactorEnabled, ct) }));

        return routes;
    }
}
