// OPS-02: Custom OpenTelemetry Metrics for MeepleAI
using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// Provides custom metrics for the MeepleAI application.
/// These metrics are exported via OpenTelemetry to Prometheus.
///
/// Domain-specific metrics live in the Metrics/ sub-directory as partial class files:
///   - Metrics/AuthMetrics.cs      — 2FA / authentication metrics
///   - Metrics/PdfMetrics.cs       — PDF upload, extraction, chunking, embedding
///   - Metrics/RagMetrics.cs       — RAG, vector search, agents, streaming, insights, LLM
///   - Metrics/SystemMetrics.cs    — cache, background jobs, API errors
///   - Metrics/UserMetrics.cs      — notifications
/// </summary>
internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Meter name for MeepleAI metrics (matches the meter name in OpenTelemetry configuration)
    /// </summary>
    public const string MeterName = "MeepleAI.Api";

    /// <summary>
    /// Meter instance for creating metrics — shared by all partial class files.
    /// </summary>
    private static readonly Meter Meter = new(MeterName, "1.0.0");
}
