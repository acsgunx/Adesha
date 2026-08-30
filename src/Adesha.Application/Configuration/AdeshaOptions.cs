using System.ComponentModel.DataAnnotations;
using Adesha.Domain.Trading;

namespace Adesha.Application.Configuration;

/// <summary>
/// Root application options, bound from the "Adesha" configuration section.
/// Validated at startup; the app fails fast if misconfigured.
/// </summary>
public sealed class AdeshaOptions
{
    public const string SectionName = "Adesha";

    /// <summary>
    /// Rule 2: defaults to Disabled everywhere. Live requires an explicit operator
    /// override in configuration (never a code default).
    /// </summary>
    public TradingMode TradingMode { get; init; } = TradingMode.Disabled;

    [Required]
    public JwtOptions Jwt { get; init; } = new();
}

public sealed class JwtOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = "adesha";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = "adesha-web";

    /// <summary>
    /// HMAC signing key. Supplied via User Secrets locally or a vault in production
    /// (Rule 3); never committed. Minimum 32 bytes for HS256.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Short-lived access token (minutes).</summary>
    [Range(1, 60)]
    public int AccessTokenLifetimeMinutes { get; init; } = 10;

    [Range(1, 90)]
    public int RefreshTokenLifetimeDays { get; init; } = 7;
}
