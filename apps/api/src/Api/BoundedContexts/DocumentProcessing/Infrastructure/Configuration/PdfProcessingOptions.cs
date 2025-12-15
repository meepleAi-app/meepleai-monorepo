

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;

/// <summary>
/// BGAI-086: Strongly-typed configuration for PDF processing pipeline
/// BGAI-087: Added large PDF optimization settings
/// </summary>
internal class PdfProcessingOptions
{
    public QualityOptions Quality { get; set; } = new();
    public long MaxFileSizeBytes { get; set; } = 104857600; // 100 MB default

    /// <summary>
    /// BGAI-087: Threshold for switching from in-memory to temp file storage (default: 50 MB)
    /// </summary>
    public long LargePdfThresholdBytes { get; set; } = 52428800; // 50 MB

    /// <summary>
    /// BGAI-087: Enable temp file strategy for large PDFs to reduce memory pressure
    /// </summary>
    public bool UseTempFileForLargePdfs { get; set; } = true;

    public ExtractorOptions Extractor { get; set; } = new();
}

/// <summary>
/// Quality validation thresholds for PDF extraction
/// </summary>
internal class QualityOptions
{
    /// <summary>
    /// Minimum acceptable quality score (0.0-1.0). Extraction fails below this threshold.
    /// </summary>
    public double MinimumThreshold { get; set; } = 0.80;

    /// <summary>
    /// Warning quality score (0.0-1.0). Triggers quality warnings between this and MinimumThreshold.
    /// </summary>
    public double WarningThreshold { get; set; } = 0.70;

    /// <summary>
    /// Minimum characters per page for quality validation
    /// </summary>
    public int MinCharsPerPage { get; set; } = 500;
}

/// <summary>
/// PDF text extractor configuration
/// </summary>
internal class ExtractorOptions
{
    public string Provider { get; set; } = "Orchestrator";
    public UnstructuredOptions Unstructured { get; set; } = new();
    public SmolDoclingOptions SmolDocling { get; set; } = new();
}

/// <summary>
/// Unstructured.io service configuration
/// </summary>
internal class UnstructuredOptions
{
    public string ApiUrl { get; set; } = "http://unstructured-service:8001";
    public int TimeoutSeconds { get; set; } = 35;
    public int MaxRetries { get; set; } = 3;
    public string Strategy { get; set; } = "fast";
    public string Language { get; set; } = "ita";
}

/// <summary>
/// SmolDocling VLM service configuration
/// </summary>
internal class SmolDoclingOptions
{
    public string ApiUrl { get; set; } = "http://smoldocling-service:8002";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}
