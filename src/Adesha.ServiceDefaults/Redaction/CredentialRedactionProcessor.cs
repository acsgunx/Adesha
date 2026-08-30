using System.Diagnostics;
using OpenTelemetry;

namespace Adesha.ServiceDefaults.Redaction;

/// <summary>
/// OpenTelemetry span processor that scrubs credential-bearing attributes before export.
/// Without this, HttpClient instrumentation can render broker Authorization headers
/// (m.Stock: "token api_key:jwtToken") in plain text in the Aspire dashboard and any
/// OTLP backend (Master Prompt Rule 3).
/// </summary>
public sealed class CredentialRedactionProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        List<KeyValuePair<string, string>>? replacements = null;

        foreach (var tag in activity.TagObjects)
        {
            if (CredentialRedactor.IsSensitiveKey(tag.Key))
            {
                (replacements ??= []).Add(new(tag.Key, CredentialRedactor.RedactedValue));
            }
            else if (tag.Value is string s && CredentialRedactor.ContainsCredentialShape(s))
            {
                (replacements ??= []).Add(new(tag.Key, CredentialRedactor.RedactValue(s)));
            }
        }

        if (replacements is not null)
        {
            foreach (var (key, value) in replacements)
            {
                activity.SetTag(key, value);
            }
        }
    }
}
