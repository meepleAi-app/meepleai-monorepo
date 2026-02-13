using System.Text.Json;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Value object for flexible entity-specific metadata in collections.
/// Stored as JSONB in database for extensibility without schema changes.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal sealed class CollectionMetadata : ValueObject
{
    /// <summary>
    /// Flexible metadata dictionary for entity-specific data.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Data { get; }

    private CollectionMetadata(IReadOnlyDictionary<string, object?> data)
    {
        Data = data ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates empty metadata.
    /// </summary>
    public static CollectionMetadata Empty() =>
        new(new Dictionary<string, object?>(StringComparer.Ordinal));

    /// <summary>
    /// Creates metadata from dictionary.
    /// </summary>
    public static CollectionMetadata Create(Dictionary<string, object?> data) =>
        new(data);

    /// <summary>
    /// Serializes metadata to JSON for database storage.
    /// </summary>
    public string ToJson() =>
        JsonSerializer.Serialize(Data);

    /// <summary>
    /// Deserializes metadata from JSON string.
    /// </summary>
    public static CollectionMetadata FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Empty();

        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        return Create(data ?? new Dictionary<string, object?>(StringComparer.Ordinal));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Compare JSON representation for equality
        yield return ToJson();
    }
}
