

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
    /// <summary>#4435: the multilingual default — the corpus is mostly English, then Italian and German.</summary>
    public const string DefaultLanguage = "eng+ita+deu";

    /// <summary>
    /// #4435: see <see cref="LanguageConfidenceThreshold"/>. <c>LanguageDetector</c>'s confidence is the
    /// share of letters in the dominant script: a Latin-script rulebook scores close to 1, so today the
    /// threshold almost never filters a real detection. What it does is keep never-detected rows (null)
    /// and empty text ("unknown", 0) from reordering the set. Above 0.8 at least four letters in five
    /// belong to the detected script; below it the text is mixed, short or garbled.
    /// </summary>
    public const double DefaultLanguageConfidenceThreshold = 0.8;

    public string Strategy { get; set; } = "fast";

    /// <summary>
    /// #4435: the languages sent to the service, as Tesseract codes joined by <c>+</c>. A known document
    /// language goes first and the rest follow; with no usable document language the set is sent as is.
    /// It also declares what the service can read: a document language outside this set is not moved
    /// first (see <c>UnstructuredLanguageResolver</c>). Keep it in step with the packs in the service's
    /// Dockerfile.
    /// </summary>
    public string Language { get; set; } = DefaultLanguage;

    /// <summary>
    /// #4435: the detected <c>Language</c> of a document is put first only when its
    /// <c>language_confidence</c> is strictly above this value (0–1). 1 disables detection; 0 trusts any
    /// non-zero detection.
    /// </summary>
    public double LanguageConfidenceThreshold { get; set; } = DefaultLanguageConfidenceThreshold;
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
