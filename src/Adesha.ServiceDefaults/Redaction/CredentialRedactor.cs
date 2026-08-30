using System.Text.RegularExpressions;

namespace Adesha.ServiceDefaults.Redaction;

/// <summary>
/// Shared credential redaction rules (Master Prompt Rule 3), applied to both Serilog
/// events and OpenTelemetry span attributes. Two layers:
/// 1. Key-based: any property/tag whose name looks credential-bearing is redacted outright.
/// 2. Value-based: string values matching known credential shapes (JWTs, Bearer/broker
///    Authorization headers, m.Stock "token api_key:jwt" format) are redacted.
/// </summary>
public static partial class CredentialRedactor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly string[] SensitiveKeyFragments =
    [
        "authorization",
        "api_key",
        "apikey",
        "api-key",
        "api_secret",
        "apisecret",
        "secret",
        "password",
        "passwd",
        "pwd",
        "token",
        "totp",
        "otp",
        "credential",
        "cookie",
        "checksum",
    ];

    // m.Stock: "Authorization: token api_key:jwtToken"; also Bearer tokens and bare JWTs.
    [GeneratedRegex(@"(token\s+[^\s:]+:\S+)|(Bearer\s+[A-Za-z0-9._\-]{8,})|(eyJ[A-Za-z0-9_\-]{4,}\.[A-Za-z0-9._\-]{4,})", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialValuePattern();

    public static bool IsSensitiveKey(string key)
    {
        foreach (var fragment in SensitiveKeyFragments)
        {
            if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsCredentialShape(string value) => CredentialValuePattern().IsMatch(value);

    /// <summary>Redacts credential-shaped substrings, leaving surrounding text intact.</summary>
    public static string RedactValue(string value) => CredentialValuePattern().Replace(value, RedactedValue);
}
