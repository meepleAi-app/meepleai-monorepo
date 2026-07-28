using System.Text.Json.Serialization;

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External.Models;

/// <summary>
/// Text chunk from Unstructured extraction
/// Matches Python schema: TextChunkSchema
/// </summary>
internal class UnstructuredChunk
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("element_type")]
    public string? ElementType { get; set; }

    [JsonPropertyName("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
