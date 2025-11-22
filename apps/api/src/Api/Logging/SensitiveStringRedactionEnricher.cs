using Serilog.Core;
using Serilog.Events;

namespace Api.Logging;

/// <summary>
/// Enricher that scans scalar string properties and redacts sensitive patterns
/// (API keys, bearer tokens, passwords in connection strings) even when values
/// are logged as scalars (not destructured).
/// </summary>
public class SensitiveStringRedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Copy keys to avoid modifying collection during enumeration
        var keys = logEvent.Properties.Keys.ToList();
        foreach (var key in keys)
        {
            if (logEvent.Properties[key] is ScalarValue scalar && scalar.Value is string s)
            {
                var redacted = SensitiveDataDestructuringPolicy.RedactInStringForLogging(s);
                if (!ReferenceEquals(redacted, s))
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(key, new ScalarValue(redacted)));
                }
            }
        }
    }
}

