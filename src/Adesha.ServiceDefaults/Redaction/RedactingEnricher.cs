using Serilog.Core;
using Serilog.Events;

namespace Adesha.ServiceDefaults.Redaction;

/// <summary>
/// Serilog enricher that rewrites credential-bearing properties before any sink sees them.
/// Recurses into structured, sequence, and dictionary values so nested payloads
/// (e.g. destructured HTTP request objects) are covered too.
/// </summary>
public sealed class RedactingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        List<(string Name, LogEventPropertyValue Value)>? replacements = null;

        foreach (var (name, value) in logEvent.Properties)
        {
            var redacted = Redact(name, value);
            if (!ReferenceEquals(redacted, value))
            {
                (replacements ??= []).Add((name, redacted));
            }
        }

        if (replacements is not null)
        {
            foreach (var (name, value) in replacements)
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, value));
            }
        }
    }

    private static LogEventPropertyValue Redact(string key, LogEventPropertyValue value)
    {
        if (CredentialRedactor.IsSensitiveKey(key))
        {
            return new ScalarValue(CredentialRedactor.RedactedValue);
        }

        switch (value)
        {
            case ScalarValue { Value: string s } when CredentialRedactor.ContainsCredentialShape(s):
                return new ScalarValue(CredentialRedactor.RedactValue(s));

            case StructureValue structure:
                {
                    List<LogEventProperty>? props = null;
                    for (var i = 0; i < structure.Properties.Count; i++)
                    {
                        var prop = structure.Properties[i];
                        var redacted = Redact(prop.Name, prop.Value);
                        if (!ReferenceEquals(redacted, prop.Value))
                        {
                            props ??= [.. structure.Properties];
                            props[i] = new LogEventProperty(prop.Name, redacted);
                        }
                    }

                    return props is null ? value : new StructureValue(props, structure.TypeTag);
                }

            case SequenceValue sequence:
                {
                    List<LogEventPropertyValue>? elements = null;
                    for (var i = 0; i < sequence.Elements.Count; i++)
                    {
                        var redacted = Redact(string.Empty, sequence.Elements[i]);
                        if (!ReferenceEquals(redacted, sequence.Elements[i]))
                        {
                            elements ??= [.. sequence.Elements];
                            elements[i] = redacted;
                        }
                    }

                    return elements is null ? value : new SequenceValue(elements);
                }

            case DictionaryValue dictionary:
                {
                    List<KeyValuePair<ScalarValue, LogEventPropertyValue>>? entries = null;
                    var i = 0;
                    foreach (var entry in dictionary.Elements)
                    {
                        var entryKey = entry.Key.Value as string ?? string.Empty;
                        var redacted = Redact(entryKey, entry.Value);
                        if (!ReferenceEquals(redacted, entry.Value))
                        {
                            entries ??= [.. dictionary.Elements];
                            entries[i] = new KeyValuePair<ScalarValue, LogEventPropertyValue>(entry.Key, redacted);
                        }

                        i++;
                    }

                    return entries is null ? value : new DictionaryValue(entries);
                }

            default:
                return value;
        }
    }
}
