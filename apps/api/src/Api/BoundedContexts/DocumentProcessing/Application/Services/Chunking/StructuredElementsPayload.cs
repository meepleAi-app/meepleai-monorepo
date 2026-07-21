using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// Versioned, tolerant persistence of the raw extraction elements so IndexPdf (which only has flat
/// ExtractedText) can rebuild the ExtractedDocument. Default JSON options (PascalCase) match the
/// existing ExtractedTables/ExtractedDiagrams columns. Reads never throw — malformed/legacy blobs
/// return null so the caller degrades to the flat null-path.
/// </summary>
internal static class StructuredElementsPayload
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record Envelope(int SchemaVersion, IReadOnlyList<ExtractedElement>? Elements);

    public static string? Serialize(IReadOnlyList<ExtractedElement>? elements)
    {
        if (elements is null || elements.Count == 0)
        {
            return null;
        }
        return JsonSerializer.Serialize(new Envelope(CurrentSchemaVersion, elements), Options);
    }

    public static IReadOnlyList<ExtractedElement>? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            var env = JsonSerializer.Deserialize<Envelope>(json, Options);
            return env?.Elements is { Count: > 0 } ? env.Elements : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
