

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

/// <summary>
/// Represents the progress state of PDF processing pipeline
/// </summary>
public class ProcessingProgress
{
    /// <summary>
    /// Current step in the processing pipeline
    /// </summary>
    public ProcessingStep CurrentStep { get; set; }

    /// <summary>
    /// Overall completion percentage (0-100)
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    /// Time elapsed since processing started
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Estimated time remaining (null if unable to estimate)
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Number of pages processed so far
    /// </summary>
    public int PagesProcessed { get; set; }

    /// <summary>
    /// Total number of pages in the PDF
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// When processing started (UTC)
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When processing completed (UTC), null if still in progress
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Calculates percent complete based on current step and pages processed
    /// </summary>
    public static int CalculatePercentComplete(ProcessingStep step, int pagesProcessed, int totalPages)
    {
        // Each step represents a percentage range:
        // Upload: 0-20%
        // Extract: 20-40% (progresses with page count)
        // Chunk: 40-60%
        // Embed: 60-80%
        // Index: 80-100%
        // Completed: 100%
        // Failed: keep last known percentage

        return step switch
        {
            ProcessingStep.Uploading => 10, // Mid-point of 0-20%
            ProcessingStep.Extracting => CalculateStepProgress(20, 40, pagesProcessed, totalPages),
            ProcessingStep.Chunking => CalculateStepProgress(40, 60, pagesProcessed, totalPages),
            ProcessingStep.Embedding => CalculateStepProgress(60, 80, pagesProcessed, totalPages),
            ProcessingStep.Indexing => CalculateStepProgress(80, 100, pagesProcessed, totalPages),
            ProcessingStep.Completed => 100,
            ProcessingStep.Failed => 0, // Return 0 for failed state
            _ => 0
        };
    }

    private static int CalculateStepProgress(int stepStart, int stepEnd, int pagesProcessed, int totalPages)
    {
        if (totalPages <= 0)
        {
            return stepStart;
        }

        var stepRange = stepEnd - stepStart;
        var pageProgress = (double)pagesProcessed / totalPages;
        return stepStart + (int)(stepRange * pageProgress);
    }

    /// <summary>
    /// Estimates time remaining based on elapsed time and current progress
    /// </summary>
    public static TimeSpan? EstimateTimeRemaining(int percentComplete, TimeSpan elapsedTime)
    {
        if (percentComplete <= 0 || percentComplete >= 100)
        {
            return null;
        }

        var elapsedSeconds = elapsedTime.TotalSeconds;
        var estimatedTotalSeconds = elapsedSeconds / (percentComplete / 100.0);
        var remainingSeconds = estimatedTotalSeconds - elapsedSeconds;

        return remainingSeconds > 0 ? TimeSpan.FromSeconds(remainingSeconds) : TimeSpan.Zero;
    }
}

/// <summary>
/// Processing pipeline steps for PDF documents
/// </summary>
public enum ProcessingStep
{
    /// <summary>
    /// File is being uploaded to server (0-20%)
    /// </summary>
    Uploading = 0,

    /// <summary>
    /// Text is being extracted from PDF pages (20-40%)
    /// </summary>
    Extracting = 1,

    /// <summary>
    /// Text is being broken into searchable chunks (40-60%)
    /// </summary>
    Chunking = 2,

    /// <summary>
    /// AI embeddings are being generated via OpenRouter (60-80%)
    /// </summary>
    Embedding = 3,

    /// <summary>
    /// Vectors are being stored in Qdrant database (80-100%)
    /// </summary>
    Indexing = 4,

    /// <summary>
    /// Processing completed successfully (100%)
    /// </summary>
    Completed = 5,

    /// <summary>
    /// Processing failed at some step
    /// </summary>
    Failed = 6
}

/// <summary>
/// JSON-serializable progress model for SSE streaming.
/// Issue #3653 - Private PDF Upload Endpoint Full Integration.
/// </summary>
public class ProcessingProgressJson
{
    /// <summary>
    /// Current processing step (uploading, extracting, chunking, embedding, indexing, completed, failed).
    /// </summary>
    public ProcessingStep Step { get; set; }

    /// <summary>
    /// Overall completion percentage (0-100, or -1 for heartbeat).
    /// </summary>
    public int Percent { get; set; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Number of pages processed (for extracting step).
    /// </summary>
    public int? Pages { get; set; }

    /// <summary>
    /// Total number of pages in the PDF.
    /// </summary>
    public int? TotalPages { get; set; }

    /// <summary>
    /// Number of chunks created (for chunking step).
    /// </summary>
    public int? Chunks { get; set; }

    /// <summary>
    /// Number of embeddings generated (for embedding step).
    /// </summary>
    public int? Embeddings { get; set; }

    /// <summary>
    /// Number of vectors indexed (for indexing step).
    /// </summary>
    public int? Indexed { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Creates a progress update from the internal ProcessingProgress model.
    /// </summary>
    public static ProcessingProgressJson FromProgress(ProcessingProgress progress)
    {
        return new ProcessingProgressJson
        {
            Step = progress.CurrentStep,
            Percent = progress.PercentComplete,
            Message = GetMessageForStep(progress.CurrentStep),
            Pages = progress.CurrentStep == ProcessingStep.Extracting ? progress.PagesProcessed : null,
            TotalPages = progress.TotalPages > 0 ? progress.TotalPages : null,
            Error = progress.ErrorMessage
        };
    }

    /// <summary>
    /// Creates a simple progress update for a specific step.
    /// </summary>
    public static ProcessingProgressJson ForStep(ProcessingStep step, int percent, string? message = null)
    {
        return new ProcessingProgressJson
        {
            Step = step,
            Percent = percent,
            Message = message ?? GetMessageForStep(step)
        };
    }

    /// <summary>
    /// Creates an extracting progress update with page information.
    /// </summary>
    public static ProcessingProgressJson ForExtracting(int pages, int totalPages)
    {
        var percent = ProcessingProgress.CalculatePercentComplete(ProcessingStep.Extracting, pages, totalPages);
        return new ProcessingProgressJson
        {
            Step = ProcessingStep.Extracting,
            Percent = percent,
            Message = $"Extracting page {pages} of {totalPages}...",
            Pages = pages,
            TotalPages = totalPages
        };
    }

    /// <summary>
    /// Creates a chunking progress update with chunk count.
    /// </summary>
    public static ProcessingProgressJson ForChunking(int chunks)
    {
        return new ProcessingProgressJson
        {
            Step = ProcessingStep.Chunking,
            Percent = 50,
            Message = $"Created {chunks} text chunks",
            Chunks = chunks
        };
    }

    /// <summary>
    /// Creates an embedding progress update with embedding count.
    /// </summary>
    public static ProcessingProgressJson ForEmbedding(int embeddings)
    {
        return new ProcessingProgressJson
        {
            Step = ProcessingStep.Embedding,
            Percent = 70,
            Message = $"Generated {embeddings} embeddings",
            Embeddings = embeddings
        };
    }

    /// <summary>
    /// Creates an indexing progress update with indexed count.
    /// </summary>
    public static ProcessingProgressJson ForIndexing(int indexed)
    {
        return new ProcessingProgressJson
        {
            Step = ProcessingStep.Indexing,
            Percent = 90,
            Message = $"Indexed {indexed} vectors",
            Indexed = indexed
        };
    }

    /// <summary>
    /// Creates a completed progress update.
    /// </summary>
    public static ProcessingProgressJson Completed()
    {
        return new ProcessingProgressJson
        {
            Step = ProcessingStep.Completed,
            Percent = 100,
            Message = "Ready to chat!"
        };
    }

    /// <summary>
    /// Creates a failed progress update with error message.
    /// </summary>
    public static ProcessingProgressJson Failed(string error)
    {
        return new ProcessingProgressJson
        {
            Step = ProcessingStep.Failed,
            Percent = 0,
            Message = "Processing failed",
            Error = error
        };
    }

    private static string GetMessageForStep(ProcessingStep step) => step switch
    {
        ProcessingStep.Uploading => "Uploading file...",
        ProcessingStep.Extracting => "Extracting text from PDF...",
        ProcessingStep.Chunking => "Creating text chunks...",
        ProcessingStep.Embedding => "Generating AI embeddings...",
        ProcessingStep.Indexing => "Indexing vectors...",
        ProcessingStep.Completed => "Ready to chat!",
        ProcessingStep.Failed => "Processing failed",
        _ => "Processing..."
    };
}
