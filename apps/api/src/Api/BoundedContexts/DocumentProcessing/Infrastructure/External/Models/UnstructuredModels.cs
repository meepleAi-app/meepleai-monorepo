using System.Text.Json.Serialization;

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External.Models;

/// <summary>
/// Response from Unstructured PDF extraction service
/// Matches Python schema: PdfExtractionResponse
/// </summary>
internal class UnstructuredResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("chunks")]
    public IList<UnstructuredChunk> Chunks { get; set; } = new List<UnstructuredChunk>();

    [JsonPropertyName("quality_score")]
    public double QualityScore { get; set; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    [JsonPropertyName("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

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

/// <summary>
/// Quality breakdown from Unstructured extraction
/// Matches Python schema: QualityBreakdownSchema
/// </summary>
internal class UnstructuredQualityBreakdown
{
    [JsonPropertyName("text_coverage_score")]
    public double TextCoverageScore { get; set; }

    [JsonPropertyName("structure_detection_score")]
    public double StructureDetectionScore { get; set; }

    [JsonPropertyName("table_detection_score")]
    public double TableDetectionScore { get; set; }

    [JsonPropertyName("page_coverage_score")]
    public double PageCoverageScore { get; set; }
}

/// <summary>
/// Error response from Unstructured service
/// Matches Python schema: ErrorResponse
/// </summary>
internal class UnstructuredErrorResponse
{
    [JsonPropertyName("error")]
    public UnstructuredErrorDetail Error { get; set; } = new();
}

/// <summary>
/// Error detail from Unstructured service
/// Matches Python schema: ErrorDetail
/// </summary>
internal class UnstructuredErrorDetail
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public IDictionary<string, object>? Details { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
