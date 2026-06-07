using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Infrastructure.DomainEventOutbox;

/// <summary>
/// Issue #1535 — shared <see cref="JsonSerializerOptions"/> for outbox payload
/// serialization and deserialization. Frozen at construction so the same options
/// instance can be reused by the DbContext writer and the processor reader.
///
/// <para>Contract:</para>
/// <list type="bullet">
///   <item><b>Property naming:</b> camelCase — keeps the persisted JSON readable
///         and aligns with the project's existing wire-format convention.</item>
///   <item><b>Enums:</b> serialized as strings (camelCase) so a renumbering of an
///         enum value cannot silently corrupt a Sent payload that hasn't been
///         re-dispatched yet.</item>
///   <item><b>Fields:</b> opt-in only — never auto-included. Domain events SHOULD
///         declare public properties; private fields are not persisted.</item>
///   <item><b>Read-only properties:</b> included so <c>init</c>-only properties
///         (the common pattern for record events) round-trip correctly.</item>
/// </list>
/// </summary>
public static class DomainEventJsonOptions
{
    /// <summary>Default options used by both the writer (DbContext) and reader (processor).</summary>
    public static readonly JsonSerializerOptions Default = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = false,
            IncludeFields = false,
            // Defensive default: outbox rows are NOT user-facing JSON; tolerate
            // unknown properties on the read path so an event schema rev (new
            // optional property) won't poison-message older Sent rows being replayed.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}
