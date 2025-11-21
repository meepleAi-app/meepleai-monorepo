using System.Diagnostics;
using OpenTelemetry;

namespace Api.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry processor that scrubs sensitive data from traces before export.
/// Prevents passwords, tokens, API keys, and other secrets from being sent to HyperDX.
/// </summary>
/// <remarks>
/// This processor runs for every span before it's exported, adding minimal overhead
/// while ensuring compliance with data privacy requirements.
///
/// Based on code review fix P1-SEC3 from hyperdx-implementation-plan.md
/// </remarks>
public class SensitiveDataProcessor : BaseProcessor<Activity>
{
    private static readonly string[] SensitiveKeys =
    {
        "password",
        "token",
        "apikey",
        "api_key",
        "secret",
        "authorization",
        "bearer",
        "credential",
        "private_key",
        "client_secret"
    };

    /// <summary>
    /// Called when a span completes, before it's exported.
    /// Scrubs any tags/attributes that contain sensitive data.
    /// </summary>
    public override void OnEnd(Activity activity)
    {
        // Create a list of tags to modify (can't modify during iteration)
        var tagsToRedact = activity.Tags
            .Where(tag => SensitiveKeys.Any(sensitiveKey =>
                tag.Key.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase)))
            .Select(tag => tag.Key)
            .ToList();

        // Redact sensitive tags
        foreach (var tagKey in tagsToRedact)
        {
            activity.SetTag(tagKey, "[REDACTED]");
        }

        // Also check custom properties (baggage)
        var baggageToRedact = activity.Baggage
            .Where(baggage => SensitiveKeys.Any(sensitiveKey =>
                baggage.Key.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase)))
            .Select(baggage => baggage.Key)
            .ToList();

        foreach (var baggageKey in baggageToRedact)
        {
            activity.SetBaggage(baggageKey, "[REDACTED]");
        }

        base.OnEnd(activity);
    }
}
