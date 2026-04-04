// OPS-02: Custom OpenTelemetry Metrics for MeepleAI
using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// Provides custom metrics for the MeepleAI application.
/// These metrics are exported via OpenTelemetry to Prometheus.
/// Domain-specific metrics live in Observability/Metrics/ as partial class files:
///   - MeepleAiMetrics.Rag.cs          — RAG, vector search, hybrid search
///   - MeepleAiMetrics.Pdf.cs          — PDF upload, extraction, chunking
///   - MeepleAiMetrics.Agent.cs        — Agent invocations, LLM tokens, streaming
///   - MeepleAiMetrics.Cache.cs        — Cache hits/misses/evictions, L1 gauge
///   - MeepleAiMetrics.Auth.cs         — 2FA TOTP and backup-code metrics
///   - MeepleAiMetrics.System.cs       — API errors, background job metrics
///   - MeepleAiMetrics.Notifications.cs — Notification creation and read events
///   - MeepleAiMetrics.Insights.cs     — AI insight generation metrics
///   - MeepleAiMetrics.LlmOperational.cs — Circuit breaker, OpenRouter gauges
///   - MeepleAiMetrics.Evaluation.cs   — RAG evaluation and grid search
/// </summary>
internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Meter name for MeepleAI metrics (matches the meter name in OpenTelemetry configuration)
    /// </summary>
    public const string MeterName = "MeepleAI.Api";

    /// <summary>
    /// Meter instance for creating metrics
    /// </summary>
    private static readonly Meter Meter = new(MeterName, "1.0.0");
}
