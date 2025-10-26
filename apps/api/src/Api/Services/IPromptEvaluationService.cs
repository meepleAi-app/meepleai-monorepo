using MeepleAI.Api.Models;

namespace MeepleAI.Api.Services;

/// <summary>
/// Service for evaluating prompt quality through automated testing
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
public interface IPromptEvaluationService
{
    /// <summary>
    /// Loads a test dataset from JSON file with schema validation
    /// </summary>
    /// <param name="datasetPath">Absolute or relative path to JSON dataset file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Loaded and validated test dataset</returns>
    Task<PromptTestDataset> LoadDatasetAsync(string datasetPath, CancellationToken ct = default);

    /// <summary>
    /// Evaluates a prompt version using a test dataset
    /// Calculates 5 metrics: Accuracy, Hallucination Rate, Confidence, Citation Correctness, Latency
    /// </summary>
    /// <param name="templateId">Template ID containing the prompt</param>
    /// <param name="versionId">Specific version ID to evaluate</param>
    /// <param name="datasetPath">Path to test dataset JSON file</param>
    /// <param name="progressCallback">Optional callback for progress updates (query index, total queries)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Evaluation results with metrics and query breakdown</returns>
    Task<PromptEvaluationResult> EvaluateAsync(
        string templateId,
        string versionId,
        string datasetPath,
        Action<int, int>? progressCallback = null,
        CancellationToken ct = default);

    /// <summary>
    /// Compares two prompt versions side-by-side using the same dataset
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="baselineVersionId">Baseline version (usually current active)</param>
    /// <param name="candidateVersionId">Candidate version to compare</param>
    /// <param name="datasetPath">Path to test dataset JSON file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A/B comparison with delta metrics and recommendation</returns>
    Task<PromptComparisonResult> CompareVersionsAsync(
        string templateId,
        string baselineVersionId,
        string candidateVersionId,
        string datasetPath,
        CancellationToken ct = default);

    /// <summary>
    /// Generates human-readable and machine-readable reports
    /// </summary>
    /// <param name="result">Evaluation result to format</param>
    /// <param name="format">Output format (markdown or json)</param>
    /// <returns>Formatted report string</returns>
    string GenerateReport(PromptEvaluationResult result, ReportFormat format = ReportFormat.Markdown);

    /// <summary>
    /// Stores evaluation results in database for historical tracking
    /// </summary>
    /// <param name="result">Evaluation result to store</param>
    /// <param name="ct">Cancellation token</param>
    Task StoreResultsAsync(PromptEvaluationResult result, CancellationToken ct = default);

    /// <summary>
    /// Retrieves historical evaluation results for a template
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of past evaluation results, ordered by execution date descending</returns>
    Task<List<PromptEvaluationResult>> GetHistoricalResultsAsync(
        string templateId,
        int limit = 10,
        CancellationToken ct = default);
}

/// <summary>
/// Report output format
/// </summary>
public enum ReportFormat
{
    /// <summary>Markdown format for human readability</summary>
    Markdown,

    /// <summary>JSON format for machine processing</summary>
    Json
}
