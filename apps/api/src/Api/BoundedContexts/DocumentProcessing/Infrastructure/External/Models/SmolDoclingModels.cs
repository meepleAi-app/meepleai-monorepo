using System.Text.Json.Serialization;

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External.Models;

/// <summary>
/// Response from SmolDocling PDF extraction service (VLM-based)
/// Matches Python schema: PdfExtractionResponse
/// </summary>
public class SmolDoclingResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("markdown")]
    public string Markdown { get; set; } = string.Empty;

    [JsonPropertyName("chunks")]
    public IList<SmolDoclingChunk> Chunks { get; set; } = new List<SmolDoclingChunk>();

    [JsonPropertyName("quality_score")]
    public double QualityScore { get; set; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    [JsonPropertyName("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

/// <summary>
/// Text chunk from SmolDocling extraction
/// Matches Python schema: TextChunkSchema
/// </summary>
public class SmolDoclingChunk
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
/// Single page extraction result from SmolDocling
/// Matches Python schema: PageResultSchema
/// </summary>
public class SmolDoclingPageResult
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("markdown_text")]
    public string MarkdownText { get; set; } = string.Empty;

    [JsonPropertyName("char_count")]
    public int CharCount { get; set; }

    [JsonPropertyName("has_tables")]
    public bool HasTables { get; set; }

    [JsonPropertyName("has_equations")]
    public bool HasEquations { get; set; }

    [JsonPropertyName("confidence_score")]
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Quality breakdown from SmolDocling extraction
/// Matches Python schema: QualityBreakdownSchema
/// </summary>
public class SmolDoclingQualityBreakdown
{
    [JsonPropertyName("text_coverage_score")]
    public double TextCoverageScore { get; set; }

    [JsonPropertyName("layout_detection_score")]
    public double LayoutDetectionScore { get; set; }

    [JsonPropertyName("confidence_score")]
    public double ConfidenceScore { get; set; }

    [JsonPropertyName("page_coverage_score")]
    public double PageCoverageScore { get; set; }
}

/// <summary>
/// Error response from SmolDocling service
/// Matches Python schema: ErrorResponse
/// </summary>
public class SmolDoclingErrorResponse
{
    [JsonPropertyName("error")]
    public SmolDoclingErrorDetail Error { get; set; } = new();
}

/// <summary>
/// Error detail from SmolDocling service
/// Matches Python schema: ErrorDetail
/// </summary>
public class SmolDoclingErrorDetail
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
