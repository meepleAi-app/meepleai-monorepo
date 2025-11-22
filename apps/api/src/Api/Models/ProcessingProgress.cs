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
