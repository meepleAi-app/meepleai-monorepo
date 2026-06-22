using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// Truncates an entity property bag so its JSON serialization fits within a byte budget.
/// Strategy (three-amigos Q2): trim enumerable fields with more than
/// <see cref="CollectionTrimThreshold"/> items down to <see cref="CollectionTrimKeep"/>, recording
/// the original counts under "_truncated"/"_original_count". If the result still exceeds the budget
/// after per-collection trimming, the bag is flagged "_oversize": true so the caller marks the
/// outbox row Failed (last_error="payload_oversize") rather than dropping the audit silently.
/// </summary>
public static class PayloadTruncator
{
    private const int CollectionTrimThreshold = 50;
    private const int CollectionTrimKeep = 10;

    /// <summary>
    /// Verbatim JSON substring emitted into a snapshot's serialized property bag when the
    /// truncated payload still exceeds the byte budget. Consumers (e.g. AuditLoggingBehavior)
    /// detect oversize snapshots via Ordinal substring match against the serialized JSON.
    ///
    /// Contract — DO NOT modify the key name, value, or formatting without updating callers:
    ///   - Dictionary key:   "_oversize"  (literal — not subject to JsonNamingPolicy renaming)
    ///   - Value:            true         (boolean)
    ///   - Serialization:    WriteIndented=false → produces "_oversize":true with NO whitespace
    /// </summary>
    public const string OversizeMarkerJson = "\"_oversize\":true";

    /// <summary>
    /// Returns true when the given serialized snapshot JSON contains the oversize marker
    /// emitted by <see cref="Truncate"/>. Ordinal comparison — case-sensitive.
    /// </summary>
    public static bool IsOversizeJson(string? snapshotJson)
        => snapshotJson is not null && snapshotJson.Contains(OversizeMarkerJson, StringComparison.Ordinal);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    public static IDictionary<string, object?> Truncate(IDictionary<string, object?> props, int maxBytes)
    {
        var truncatedFields = new List<string>();
        var originalCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var key in props.Keys.ToList())
        {
            var value = props[key];
            if (value is string || value is not IEnumerable enumerable)
                continue;

            var list = enumerable.Cast<object?>().ToList();
            if (list.Count <= CollectionTrimThreshold)
                continue;

            props[key] = list.Take(CollectionTrimKeep).ToList();
            truncatedFields.Add(key);
            originalCounts[key] = list.Count;
        }

        if (truncatedFields.Count > 0)
        {
            props["_truncated"] = truncatedFields;
            props["_original_count"] = originalCounts;
        }

        var size = JsonSerializer.SerializeToUtf8Bytes(props, JsonOpts).Length;
        if (size > maxBytes)
            props["_oversize"] = true;

        return props;
    }
}
