// OPS-02: Custom Activity Sources for OpenTelemetry Distributed Tracing
using System.Diagnostics;

namespace Api.Observability;

/// <summary>
/// Provides custom Activity Sources for distributed tracing in the MeepleAI application.
/// Activity Sources are used to create custom spans (traces) that are exported to Jaeger.
///
/// Note: This is separate from MeepleAiMetrics which handles Meters for metrics/counters.
/// - ActivitySource = Distributed Tracing (spans, traces)
/// - Meter = Metrics (counters, histograms, gauges)
/// </summary>
public static class MeepleAiActivitySources
{
    /// <summary>
    /// Main Activity Source for the MeepleAI API.
    /// This is the primary source for all application-level traces.
    /// </summary>
    public const string ApiSourceName = "MeepleAI.Api";

    /// <summary>
    /// Activity Source for RAG (Retrieval-Augmented Generation) operations.
    /// Used to trace AI question answering and context retrieval.
    /// </summary>
    public const string RagSourceName = "MeepleAI.Rag";

    /// <summary>
    /// Activity Source for vector search operations.
    /// Used to trace Qdrant vector database queries and indexing.
    /// </summary>
    public const string VectorSearchSourceName = "MeepleAI.VectorSearch";

    /// <summary>
    /// Activity Source for PDF processing operations.
    /// Used to trace PDF uploads, text extraction, and chunking.
    /// </summary>
    public const string PdfProcessingSourceName = "MeepleAI.PdfProcessing";

    /// <summary>
    /// Activity Source for cache operations.
    /// Used to trace Redis cache interactions.
    /// </summary>
    public const string CacheSourceName = "MeepleAI.Cache";

    // Activity Source instances (singleton, created once)
    private static readonly ActivitySource ApiSource = new(ApiSourceName, "1.0.0");
    private static readonly ActivitySource RagSource = new(RagSourceName, "1.0.0");
    private static readonly ActivitySource VectorSearchSource = new(VectorSearchSourceName, "1.0.0");
    private static readonly ActivitySource PdfProcessingSource = new(PdfProcessingSourceName, "1.0.0");
    private static readonly ActivitySource CacheSource = new(CacheSourceName, "1.0.0");

    /// <summary>
    /// Gets the main API Activity Source.
    /// Use this for general application-level tracing.
    /// </summary>
    public static ActivitySource Api => ApiSource;

    /// <summary>
    /// Gets the RAG Activity Source.
    /// Use this in RagService and LlmService for tracing AI operations.
    /// </summary>
    public static ActivitySource Rag => RagSource;

    /// <summary>
    /// Gets the Vector Search Activity Source.
    /// Use this in QdrantService for tracing vector search and indexing.
    /// </summary>
    public static ActivitySource VectorSearch => VectorSearchSource;

    /// <summary>
    /// Gets the PDF Processing Activity Source.
    /// Use this in PDF-related services for tracing document processing.
    /// </summary>
    public static ActivitySource PdfProcessing => PdfProcessingSource;

    /// <summary>
    /// Gets the Cache Activity Source.
    /// Use this in cache services for tracing Redis operations.
    /// </summary>
    public static ActivitySource Cache => CacheSource;

    /// <summary>
    /// Returns all Activity Source names for OpenTelemetry configuration.
    /// Use this when setting up .WithTracing().AddSource(...) in Program.cs.
    /// </summary>
    public static string[] GetAllSourceNames() => new[]
    {
        ApiSourceName,
        RagSourceName,
        VectorSearchSourceName,
        PdfProcessingSourceName,
        CacheSourceName
    };
}
