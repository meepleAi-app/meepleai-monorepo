// OPS-02: PDF processing and document extraction metrics
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for total PDF uploads
    /// </summary>
    public static readonly Counter<long> PdfUploadTotal = Meter.CreateCounter<long>(
        name: "meepleai.pdf.upload.total",
        unit: "uploads",
        description: "Total number of PDF uploads");

    /// <summary>
    /// Counter for PDF upload attempts by status (success/validation_failed/storage_failed)
    /// </summary>
    public static readonly Counter<long> PdfUploadAttempts = Meter.CreateCounter<long>(
        name: "meepleai.pdf.upload.attempts",
        unit: "attempts",
        description: "PDF upload attempts by status");

    /// <summary>
    /// Histogram for PDF file size distribution in bytes
    /// </summary>
    public static readonly Histogram<long> PdfFileSizeBytes = Meter.CreateHistogram<long>(
        name: "meepleai.pdf.file.size",
        unit: "bytes",
        description: "PDF file size distribution");

    /// <summary>
    /// Histogram for PDF processing duration in milliseconds (full pipeline)
    /// </summary>
    public static readonly Histogram<double> PdfProcessingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.processing.duration",
        unit: "ms",
        description: "PDF processing duration in milliseconds");

    /// <summary>
    /// Counter for pages processed
    /// </summary>
    public static readonly Counter<long> PdfPagesProcessed = Meter.CreateCounter<long>(
        name: "meepleai.pdf.pages.processed",
        unit: "pages",
        description: "Total number of PDF pages processed");

    /// <summary>
    /// Counter for PDF extraction errors
    /// </summary>
    public static readonly Counter<long> PdfExtractionErrors = Meter.CreateCounter<long>(
        name: "meepleai.pdf.extraction.errors",
        unit: "errors",
        description: "Total number of PDF extraction errors");

    /// <summary>
    /// Counter for 3-stage extraction attempts (unstructured/smoldocling/docnet)
    /// </summary>
    public static readonly Counter<long> PdfExtractionStageAttempts = Meter.CreateCounter<long>(
        name: "meepleai.pdf.extraction.stage.attempts",
        unit: "attempts",
        description: "Extraction attempts by stage");

    /// <summary>
    /// Counter for successful extractions by stage
    /// </summary>
    public static readonly Counter<long> PdfExtractionStageSuccess = Meter.CreateCounter<long>(
        name: "meepleai.pdf.extraction.stage.success",
        unit: "successes",
        description: "Successful extractions by stage");

    /// <summary>
    /// Histogram for extraction duration by stage in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfExtractionStageDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.extraction.stage.duration",
        unit: "ms",
        description: "Extraction duration by stage");

    /// <summary>
    /// Histogram for PDF extraction quality scores (0.0-1.0)
    /// </summary>
    public static readonly Histogram<double> PdfQualityScore = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.quality.score",
        unit: "score",
        description: "PDF extraction quality scores");

    /// <summary>
    /// Histogram for PDF chunking duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfChunkingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.chunking.duration",
        unit: "ms",
        description: "Text chunking duration");

    /// <summary>
    /// Histogram for number of chunks generated per document
    /// </summary>
    public static readonly Histogram<int> PdfChunkCount = Meter.CreateHistogram<int>(
        name: "meepleai.pdf.chunks.count",
        unit: "chunks",
        description: "Number of chunks generated per document");

    /// <summary>
    /// Histogram for embedding generation duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfEmbeddingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.embedding.duration",
        unit: "ms",
        description: "Embedding generation duration");

    /// <summary>
    /// Histogram for Qdrant indexing duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfIndexingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.indexing.duration",
        unit: "ms",
        description: "Qdrant indexing duration");

    /// <summary>
    /// Records PDF upload attempt with status
    /// </summary>
    public static void RecordPdfUploadAttempt(string status, long? fileSizeBytes = null)
    {
        var tags = new TagList { { "status", status } };
        PdfUploadAttempts.Add(1, tags);

        if (fileSizeBytes.HasValue)
        {
            PdfFileSizeBytes.Record(fileSizeBytes.Value, tags);
        }
    }

    /// <summary>
    /// Records PDF extraction stage attempt and duration
    /// </summary>
    public static void RecordPdfExtractionStage(
        string stageName,
        bool success,
        double durationMs,
        double? qualityScore = null)
    {
        var tags = new TagList
        {
            { "stage", stageName.ToLowerInvariant() },
            { "success", success }
        };

        PdfExtractionStageAttempts.Add(1, tags);
        PdfExtractionStageDuration.Record(durationMs, tags);

        if (success)
        {
            PdfExtractionStageSuccess.Add(1, tags);

            if (qualityScore.HasValue)
            {
                var qualityTags = new TagList { { "stage", stageName.ToLowerInvariant() } };
                PdfQualityScore.Record(qualityScore.Value, qualityTags);
            }
        }
    }

    /// <summary>
    /// Records PDF processing pipeline step duration
    /// </summary>
    public static void RecordPdfPipelineStep(
        string step,
        double durationMs,
        int? count = null)
    {
        var tags = new TagList { { "step", step.ToLowerInvariant() } };

        switch (step.ToLowerInvariant())
        {
            case "chunking":
                PdfChunkingDuration.Record(durationMs, tags);
                if (count.HasValue)
                {
                    PdfChunkCount.Record(count.Value, tags);
                }
                break;

            case "embedding":
                PdfEmbeddingDuration.Record(durationMs, tags);
                break;

            case "indexing":
                PdfIndexingDuration.Record(durationMs, tags);
                break;

            case "extraction":
                // Use PdfProcessingDuration for overall extraction
                PdfProcessingDuration.Record(durationMs, tags);
                break;
        }
    }
}
