using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Aggregate of <see cref="FieldProvenance"/> entries for a CatalogSeedDraft.
/// Provides merging (primary + fallback chain) and JSON serialization for
/// persistence on <c>CatalogSeedDraftEntity.ProvenanceJson</c>.
/// </summary>
public sealed class CatalogSeedProvenance
{
    private readonly Dictionary<string, FieldProvenance> _fields;

    public CatalogSeedProvenance(IReadOnlyDictionary<string, FieldProvenance> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = new Dictionary<string, FieldProvenance>(fields, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, FieldProvenance> Fields => _fields;

    public T? GetValue<T>(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var fp))
        {
            return default;
        }

        // Fast path: in-memory value already matches the requested type
        if (fp.Value is T t)
        {
            return t;
        }

        // Slow path: deserialized JsonElement needs conversion (FromJson scenario)
        if (fp.Value is JsonElement je)
        {
            try
            {
                return je.Deserialize<T>(JsonOpts);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        return default;
    }

    public string? GetProvider(string fieldName)
        => _fields.TryGetValue(fieldName, out var fp) ? fp.Provider : null;

    /// <summary>
    /// Merges primary and fallback dictionaries. Primary entries win on conflict;
    /// fallback fills missing fields only.
    /// </summary>
    public static CatalogSeedProvenance Merge(
        IReadOnlyDictionary<string, FieldProvenance> primary,
        IReadOnlyDictionary<string, FieldProvenance> fallback)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(fallback);

        var merged = new Dictionary<string, FieldProvenance>(primary, StringComparer.Ordinal);
        foreach (var kv in fallback)
        {
            if (!merged.ContainsKey(kv.Key))
            {
                merged[kv.Key] = kv.Value;
            }
        }
        return new CatalogSeedProvenance(merged);
    }

    public string ToJson() => JsonSerializer.Serialize(_fields, JsonOpts);

    public static CatalogSeedProvenance FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var dict = JsonSerializer.Deserialize<Dictionary<string, FieldProvenance>>(json, JsonOpts)
                   ?? new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);
        return new CatalogSeedProvenance(dict);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
