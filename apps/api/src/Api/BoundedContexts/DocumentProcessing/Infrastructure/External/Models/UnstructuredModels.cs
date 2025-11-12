using System.Text.Json.Serialization;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External.Models;

/// <summary>
/// Response from Unstructured PDF extraction service
/// Matches Python schema: PdfExtractionResponse
/// </summary>
public class UnstructuredResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("chunks")]
    public List<UnstructuredChunk> Chunks { get; set; } = new();

    [JsonPropertyName("quality_score")]
    public double QualityScore { get; set; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Text chunk from Unstructured extraction
/// Matches Python schema: TextChunkSchema
/// </summary>
public class UnstructuredChunk
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("element_type")]
    public string? ElementType { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Quality breakdown from Unstructured extraction
/// Matches Python schema: QualityBreakdownSchema
/// </summary>
public class UnstructuredQualityBreakdown
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
public class UnstructuredErrorResponse
{
    [JsonPropertyName("error")]
    public UnstructuredErrorDetail Error { get; set; } = new();
}

/// <summary>
/// Error detail from Unstructured service
/// Matches Python schema: ErrorDetail
/// </summary>
public class UnstructuredErrorDetail
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public Dictionary<string, object>? Details { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
