using System.Text.Json;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #3866: <c>audit_outbox.payload_json</c> is a <c>jsonb</c> column, and Postgres does not
/// store the bytes it is handed — it parses the document and renders it back in its own normal
/// form, which puts a space after every colon. A test that substring-matches
/// <c>"\"Resource\":\"X\""</c> therefore matches the string the application serialized, never the
/// one the database holds.
///
/// <para>
/// It passed for years because a tracking test context answered the read from the change tracker
/// with the in-memory instance the writer had built. With the production NoTracking default
/// (PERF-06) the read goes to the row, and the difference becomes visible: the assertions were
/// pinned to a serialization detail of the writer, not to the payload.
/// </para>
///
/// <para>
/// These helpers match on the parsed document instead. <c>Details</c> is itself a JSON document
/// carried as a string value — Postgres treats it as opaque text and does not reformat it — so it
/// is parsed a second time.
/// </para>
/// </summary>
internal static class AuditOutboxPayloadJson
{
    /// <summary>Reads a top-level string property, or null when absent or not a string.</summary>
    public static string? Property(string payloadJson, string name)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
    }

    /// <summary>True when the top-level string property equals <paramref name="value"/>.</summary>
    public static bool Has(string payloadJson, string name, string value)
        => string.Equals(Property(payloadJson, name), value, StringComparison.Ordinal);

    /// <summary>True when the payload describes the given audited resource.</summary>
    public static bool IsResource(string payloadJson, string resource)
        => Has(payloadJson, "Resource", resource);

    /// <summary>
    /// Reads a property of the nested <c>Details</c> document as text. Numbers come back in their
    /// JSON form (<c>68448</c>), strings without quotes.
    /// </summary>
    public static string? Detail(string payloadJson, string name)
    {
        var details = Property(payloadJson, "Details");
        if (string.IsNullOrEmpty(details))
        {
            return null;
        }

        using var document = JsonDocument.Parse(details);
        return document.RootElement.TryGetProperty(name, out var element)
            ? element.ToString()
            : null;
    }
}
