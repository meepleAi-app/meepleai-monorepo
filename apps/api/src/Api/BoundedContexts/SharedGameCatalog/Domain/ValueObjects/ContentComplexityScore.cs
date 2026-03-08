namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Routing decision for rulebook analysis based on content complexity.
/// Issue #5451: Content complexity routing.
/// </summary>
public enum RoutingDecision
{
    Synchronous,
    Background
}

/// <summary>
/// Value object representing the complexity score of rulebook content.
/// Used to determine sync/async routing for analysis.
/// Formula: complexity = (pageCount/40)*0.3 + (tableCount/20)*0.25 + imageRatio*0.25 + (ocrRequired ? 0.2 : 0)
/// Issue #5451: Content complexity routing.
/// </summary>
public sealed class ContentComplexityScore
{
    private const decimal BackgroundThreshold = 0.4m;
    private const int CharsPerPage = 3000; // Average chars per rulebook page
    private const int MaxPageNorm = 40;
    private const int MaxTableNorm = 20;

    /// <summary>
    /// The computed complexity score (0-1).
    /// </summary>
    public decimal Score { get; }

    /// <summary>
    /// Estimated page count from text length.
    /// </summary>
    public int EstimatedPageCount { get; }

    /// <summary>
    /// Detected table count from text patterns.
    /// </summary>
    public int DetectedTableCount { get; }

    /// <summary>
    /// Estimated image ratio (0-1) based on non-text content markers.
    /// </summary>
    public decimal ImageRatio { get; }

    /// <summary>
    /// Whether OCR was likely required (detected from content patterns).
    /// </summary>
    public bool OcrRequired { get; }

    /// <summary>
    /// The routing decision based on the complexity score.
    /// </summary>
    public RoutingDecision Decision => Score > BackgroundThreshold
        ? RoutingDecision.Background
        : RoutingDecision.Synchronous;

    private ContentComplexityScore(
        decimal score,
        int estimatedPageCount,
        int detectedTableCount,
        decimal imageRatio,
        bool ocrRequired)
    {
        Score = Math.Clamp(score, 0m, 1m);
        EstimatedPageCount = estimatedPageCount;
        DetectedTableCount = detectedTableCount;
        ImageRatio = Math.Clamp(imageRatio, 0m, 1m);
        OcrRequired = ocrRequired;
    }

    /// <summary>
    /// Computes complexity score from rulebook text content.
    /// </summary>
    public static ContentComplexityScore ComputeFromText(string rulebookContent)
    {
        if (string.IsNullOrWhiteSpace(rulebookContent))
        {
            return new ContentComplexityScore(0m, 0, 0, 0m, false);
        }

        var estimatedPageCount = Math.Max(1, rulebookContent.Length / CharsPerPage);
        var detectedTableCount = CountTables(rulebookContent);
        var imageRatio = EstimateImageRatio(rulebookContent);
        var ocrRequired = DetectOcrMarkers(rulebookContent);

        var pageComponent = Math.Min(1.0m, (decimal)estimatedPageCount / MaxPageNorm) * 0.3m;
        var tableComponent = Math.Min(1.0m, (decimal)detectedTableCount / MaxTableNorm) * 0.25m;
        var imageComponent = imageRatio * 0.25m;
        var ocrComponent = ocrRequired ? 0.2m : 0m;

        var score = pageComponent + tableComponent + imageComponent + ocrComponent;

        return new ContentComplexityScore(score, estimatedPageCount, detectedTableCount, imageRatio, ocrRequired);
    }

    /// <summary>
    /// Creates a complexity score from explicit metadata (e.g., from PDF processor).
    /// </summary>
    public static ContentComplexityScore CreateFromMetadata(
        int pageCount,
        int tableCount,
        decimal imageRatio,
        bool ocrRequired)
    {
        var pageComponent = Math.Min(1.0m, (decimal)pageCount / MaxPageNorm) * 0.3m;
        var tableComponent = Math.Min(1.0m, (decimal)tableCount / MaxTableNorm) * 0.25m;
        var imageComponent = Math.Clamp(imageRatio, 0m, 1m) * 0.25m;
        var ocrComponent = ocrRequired ? 0.2m : 0m;

        var score = pageComponent + tableComponent + imageComponent + ocrComponent;

        return new ContentComplexityScore(score, pageCount, tableCount, imageRatio, ocrRequired);
    }

    private static int CountTables(string content)
    {
        // Count table-like patterns: lines starting and ending with pipe
        var pipeRowCount = 0;
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 2 && trimmed[0] == '|' && trimmed[^1] == '|')
            {
                pipeRowCount++;
            }
        }

        // Divide by approximate rows per table
        return Math.Max(0, pipeRowCount / 3);
    }

    private static decimal EstimateImageRatio(string content)
    {
        // Detect image placeholders from PDF extraction
        var imageMarkers = new[] { "[image]", "[figure]", "[diagram]", "[illustration]", "[photo]", "[table image]" };
        var markerCount = imageMarkers.Sum(marker =>
            content.Split(new[] { marker }, StringSplitOptions.None).Length - 1);

        // Estimate: each image marker represents ~500 chars of content that was an image
        var estimatedImageChars = markerCount * 500;
        if (content.Length == 0) return 0m;

        return Math.Min(1.0m, (decimal)estimatedImageChars / content.Length);
    }

    private static bool DetectOcrMarkers(string content)
    {
        // OCR indicators: high frequency of single-char words, garbled text patterns
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 100) return false;

        var singleCharWords = words.Count(w => w.Length == 1 && !char.IsDigit(w[0]) && w[0] != 'a' && w[0] != 'I');
        var ratio = (decimal)singleCharWords / words.Length;

        // High single-char word ratio suggests OCR artifacts
        return ratio > 0.05m;
    }

    public override string ToString() =>
        $"Complexity: {Score:F4} (pages={EstimatedPageCount}, tables={DetectedTableCount}, " +
        $"imageRatio={ImageRatio:F2}, ocr={OcrRequired}) → {Decision}";
}
