using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// OpenTelemetry metrics for Prompt Management system.
/// ADMIN-01 Phase 5: Deployment and Monitoring.
/// </summary>
internal static class PromptManagementMetrics
{
    private static readonly Meter Meter = new("MeepleAI.PromptManagement", "1.0.0");

    /// <summary>
    /// Counter for prompt cache hits
    /// </summary>
    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "meepleai.prompt.cache.hits",
        unit: "{hits}",
        description: "Number of successful cache retrievals for prompts");

    /// <summary>
    /// Counter for prompt cache misses
    /// </summary>
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "meepleai.prompt.cache.misses",
        unit: "{misses}",
        description: "Number of cache misses requiring database lookup");

    /// <summary>
    /// Histogram for prompt retrieval duration
    /// </summary>
    public static readonly Histogram<double> RetrievalDuration = Meter.CreateHistogram<double>(
        "meepleai.prompt.retrieval.duration",
        unit: "ms",
        description: "Duration of prompt retrieval operations (cache or database)");

    /// <summary>
    /// Counter for prompt activations
    /// </summary>
    public static readonly Counter<long> ActivationTotal = Meter.CreateCounter<long>(
        "meepleai.prompt.activation.total",
        unit: "{activations}",
        description: "Total number of prompt version activations");

    /// <summary>
    /// Histogram for prompt activation duration
    /// </summary>
    public static readonly Histogram<double> ActivationDuration = Meter.CreateHistogram<double>(
        "meepleai.prompt.activation.duration",
        unit: "ms",
        description: "Duration of prompt activation operations");

    /// <summary>
    /// Counter for prompt evaluations
    /// </summary>
    public static readonly Counter<long> EvaluationTotal = Meter.CreateCounter<long>(
        "meepleai.prompt.evaluation.total",
        unit: "{evaluations}",
        description: "Total number of prompt evaluations run");

    /// <summary>
    /// Histogram for evaluation duration
    /// </summary>
    public static readonly Histogram<double> EvaluationDuration = Meter.CreateHistogram<double>(
        "meepleai.prompt.evaluation.duration",
        unit: "ms",
        description: "Duration of full prompt evaluation runs");

    /// <summary>
    /// Gauge for evaluation accuracy metric
    /// </summary>
    public static readonly ObservableGauge<double> EvaluationAccuracy = Meter.CreateObservableGauge<double>(
        "meepleai.prompt.evaluation.accuracy",
        () => new Measurement<double>(0), // Will be updated by service
        unit: "%",
        description: "Latest prompt evaluation accuracy percentage");

    /// <summary>
    /// Gauge for evaluation hallucination rate
    /// </summary>
    public static readonly ObservableGauge<double> EvaluationHallucinationRate = Meter.CreateObservableGauge<double>(
        "meepleai.prompt.evaluation.hallucination_rate",
        () => new Measurement<double>(0),
        unit: "%",
        description: "Latest prompt evaluation hallucination rate percentage");

    /// <summary>
    /// Gauge for active prompts count
    /// </summary>
    public static readonly ObservableGauge<int> ActivePromptsCount = Meter.CreateObservableGauge<int>(
        "meepleai.prompt.active.count",
        () => new Measurement<int>(0),
        unit: "{prompts}",
        description: "Number of active prompt templates");

    /// <summary>
    /// Helper method to record prompt retrieval
    /// </summary>
    public static void RecordPromptRetrieval(double durationMs, bool cacheHit, string templateName)
    {
        RetrievalDuration.Record(durationMs,
            new KeyValuePair<string, object?>("template", templateName),
            new KeyValuePair<string, object?>("cache_hit", cacheHit));

        if (cacheHit)
            CacheHits.Add(1, new KeyValuePair<string, object?>("template", templateName));
        else
            CacheMisses.Add(1, new KeyValuePair<string, object?>("template", templateName));
    }

    /// <summary>
    /// Helper method to record prompt activation
    /// </summary>
    public static void RecordPromptActivation(double durationMs, string templateId, string versionId, bool success)
    {
        ActivationTotal.Add(1,
            new KeyValuePair<string, object?>("template_id", templateId),
            new KeyValuePair<string, object?>("version_id", versionId),
            new KeyValuePair<string, object?>("success", success));

        ActivationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("template_id", templateId),
            new KeyValuePair<string, object?>("success", success));
    }

    /// <summary>
    /// Helper method to record evaluation completion
    /// </summary>
    public static void RecordEvaluation(
        double durationMs,
        string templateId,
        string versionId,
        bool passed,
        double accuracy,
        double hallucinationRate)
    {
        EvaluationTotal.Add(1,
            new KeyValuePair<string, object?>("template_id", templateId),
            new KeyValuePair<string, object?>("version_id", versionId),
            new KeyValuePair<string, object?>("passed", passed));

        EvaluationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("template_id", templateId),
            new KeyValuePair<string, object?>("passed", passed));
    }
}
