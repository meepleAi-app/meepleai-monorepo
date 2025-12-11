using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service for PDF extraction quality validation and threshold enforcement
/// Validates extraction results against quality standards and generates quality reports
/// </summary>
/// <remarks>
/// Issue #951: BGAI-012 - PDF Quality Validation
/// Enforces quality threshold ≥0.80 as per ADR-003
/// Provides detailed quality assessment for monitoring and debugging
/// </remarks>
public class PdfQualityValidationDomainService
{
    private readonly ILogger<PdfQualityValidationDomainService> _logger;
    private readonly IConfiguration _configuration;

    // Default quality thresholds (configurable)
    private const double DefaultMinimumQualityThreshold = 0.80;

    public PdfQualityValidationDomainService(
        ILogger<PdfQualityValidationDomainService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Validates extraction result against quality thresholds
    /// </summary>
    /// <param name="result">Extraction result to validate</param>
    /// <param name="sourceName">Source extractor name (for logging)</param>
    /// <param name="requestId">Request ID for tracking</param>
    /// <returns>Quality validation result with pass/fail and recommendations</returns>
    public PdfQualityValidationResult ValidateExtractionQuality(
        TextExtractionResult result,
        string sourceName,
        string requestId)
    {
        if (!result.Success)
        {
            _logger.LogWarning(
                "[{RequestId}] Quality validation skipped - extraction failed: {Error}",
                requestId, result.ErrorMessage);

            return PdfQualityValidationResult.CreateFailed(
                sourceName,
                "Extraction failed before quality assessment",
                result.ErrorMessage ?? "Unknown error");
        }

        var qualityScore = MapQualityToScore(result.Quality);
        var threshold = GetQualityThreshold();

        var passesThreshold = qualityScore >= threshold;

        _logger.LogInformation(
            "[{RequestId}] Quality validation - Source: {Source}, Score: {Score:F2}, Threshold: {Threshold:F2}, Result: {Result}",
            requestId, sourceName, qualityScore, threshold, passesThreshold ? "PASS" : "FAIL");

        // Generate quality report
        var report = GenerateQualityReport(result, sourceName, qualityScore, threshold, requestId);

        if (!passesThreshold)
        {
            _logger.LogWarning(
                "[{RequestId}] Quality below threshold - Score: {Score:F2} < {Threshold:F2}, Recommendation: {Recommendation}",
                requestId, qualityScore, threshold, report.Recommendation);
        }

        return PdfQualityValidationResult.Create(
            passed: passesThreshold,
            sourceName: sourceName,
            qualityScore: qualityScore,
            threshold: threshold,
            report: report);
    }

    /// <summary>
    /// Generates detailed quality assessment report
    /// </summary>
    private PdfQualityReport GenerateQualityReport(
        TextExtractionResult result,
        string sourceName,
        double qualityScore,
        double threshold,
        string requestId)
    {
        var qualityLevel = DetermineQualityLevel(qualityScore);
        var recommendation = GenerateRecommendation(qualityScore, result.Quality, sourceName);

        var metrics = new PdfQualityMetrics(
            TotalScore: qualityScore,
            TextCoverageScore: CalculateTextCoverage(result),
            StructureScore: CalculateStructureScore(result),
            CompletenessScore: CalculateCompletenessScore(result),
            PageCount: result.PageCount,
            CharacterCount: result.CharacterCount,
            CharsPerPage: result.PageCount > 0 ? result.CharacterCount / (double)result.PageCount : 0);

        return new PdfQualityReport(
            RequestId: requestId,
            SourceExtractor: sourceName,
            QualityLevel: qualityLevel,
            Metrics: metrics,
            PassesThreshold: qualityScore >= threshold,
            Threshold: threshold,
            Recommendation: recommendation,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Maps ExtractionQuality enum to numeric score (0.0-1.0)
    /// </summary>
    private static double MapQualityToScore(ExtractionQuality quality)
    {
        return quality switch
        {
            ExtractionQuality.High => 0.85,
            ExtractionQuality.Medium => 0.70,
            ExtractionQuality.Low => 0.50,
            ExtractionQuality.VeryLow => 0.25,
            _ => 0.0
        };
    }

    /// <summary>
    /// Determines quality level from score
    /// </summary>
    private string DetermineQualityLevel(double score)
    {
        return score switch
        {
            >= 0.90 => "Excellent",
            >= 0.80 => "Good",
            >= 0.70 => "Acceptable",
            >= 0.50 => "Poor",
            _ => "Critical"
        };
    }

    /// <summary>
    /// Generates actionable recommendation based on quality score
    /// </summary>
    private string GenerateRecommendation(double score, string source)
    {
        if (score >= 0.80)
        {
            return "Quality meets threshold - suitable for RAG pipeline";
        }

        if (score >= 0.70)
        {
            return $"Quality acceptable but below optimal threshold - consider fallback extraction if {source} == Stage1";
        }

        if (score >= 0.50)
        {
            return "Quality poor - fallback to next stage recommended";
        }

        return "Quality critical - document may be corrupted or incompatible";
    }

    /// <summary>
    /// Calculates text coverage score based on characters per page
    /// </summary>
    private double CalculateTextCoverage(TextExtractionResult result)
    {
        if (result.PageCount == 0) return 0.0;

        var charsPerPage = result.CharacterCount / (double)result.PageCount;
        var minChars = _configuration.GetValue<int>("PdfProcessing:Quality:MinCharsPerPage", 500);
        var idealChars = minChars * 2;

        if (charsPerPage < minChars)
        {
            return Math.Min(charsPerPage / minChars * 0.5, 0.5);
        }

        if (charsPerPage >= idealChars)
        {
            return 1.0;
        }

        return 0.5 + (charsPerPage - minChars) / (idealChars - minChars) * 0.5;
    }

    /// <summary>
    /// Estimates structure score from extraction quality enum
    /// </summary>
    private static double CalculateStructureScore(TextExtractionResult result)
    {
        // Structure detection implicit in quality enum
        return result.Quality switch
        {
            ExtractionQuality.High => 0.90,
            ExtractionQuality.Medium => 0.70,
            ExtractionQuality.Low => 0.40,
            ExtractionQuality.VeryLow => 0.10,
            _ => 0.0
        };
    }

    /// <summary>
    /// Calculates completeness score
    /// </summary>
    private static double CalculateCompletenessScore(TextExtractionResult result)
    {
        if (result.PageCount == 0) return 0.0;

        // Assume full page coverage if extraction succeeded
        // (individual extractors handle page-level validation)
        return result.Success ? 1.0 : 0.0;
    }

    /// <summary>
    /// Gets configured quality threshold
    /// </summary>
    private double GetQualityThreshold()
    {
        return _configuration.GetValue<double>(
            "PdfProcessing:Quality:MinimumThreshold",
            DefaultMinimumQualityThreshold);
    }

    /// <summary>
    /// Validates if quality meets minimum acceptance criteria
    /// </summary>
    public bool MeetsMinimumQuality(ExtractionQuality quality)
    {
        var score = MapQualityToScore(quality);
        var threshold = GetQualityThreshold();
        return score >= threshold;
    }

    /// <summary>
    /// Validates if quality score meets threshold (numeric version)
    /// </summary>
    public bool MeetsMinimumQuality(double qualityScore)
    {
        var threshold = GetQualityThreshold();
        return qualityScore >= threshold;
    }
}

/// <summary>
/// Result of PDF quality validation
/// </summary>
public record PdfQualityValidationResult(
    bool Passed,
    string SourceExtractor,
    double QualityScore,
    double Threshold,
    PdfQualityReport Report)
{
    public static PdfQualityValidationResult Create(
        bool passed,
        string sourceName,
        double qualityScore,
        double threshold,
        PdfQualityReport report)
    {
        return new PdfQualityValidationResult(
            Passed: passed,
            SourceExtractor: sourceName,
            QualityScore: qualityScore,
            Threshold: threshold,
            Report: report);
    }

    public static PdfQualityValidationResult CreateFailed(
        string sourceName,
        string reason,
        string errorMessage)
    {
        var failedReport = new PdfQualityReport(
            RequestId: Guid.NewGuid().ToString(),
            SourceExtractor: sourceName,
            QualityLevel: "Failed",
            Metrics: new PdfQualityMetrics(0, 0, 0, 0, 0, 0, 0),
            PassesThreshold: false,
            Threshold: 0.80,
            Recommendation: $"Extraction failed: {reason}",
            Timestamp: DateTime.UtcNow);

        return new PdfQualityValidationResult(
            Passed: false,
            SourceExtractor: sourceName,
            QualityScore: 0.0,
            Threshold: 0.80,
            Report: failedReport);
    }
}

/// <summary>
/// Detailed PDF quality assessment report
/// </summary>
public record PdfQualityReport(
    string RequestId,
    string SourceExtractor,
    string QualityLevel,
    PdfQualityMetrics Metrics,
    bool PassesThreshold,
    double Threshold,
    string Recommendation,
    DateTime Timestamp);

/// <summary>
/// PDF extraction quality metrics breakdown
/// </summary>
public record PdfQualityMetrics(
    double TotalScore,
    double TextCoverageScore,
    double StructureScore,
    double CompletenessScore,
    int PageCount,
    int CharacterCount,
    double CharsPerPage);
