using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Models;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #5106: the progress of a PDF that has none recorded (<c>ProcessingProgressJson</c> empty: a seeded PDF, or one
/// processed before the pipeline wrote it), derived from its processing state. Without it
/// <c>GET /pdfs/{id}/progress</c> answered 200 with an empty body, which the admin screens read as an error.
/// </summary>
internal static class PdfProgressFromState
{
    public static ProcessingProgress Derive(
        string processingState, string? processingError, DateTime? processedAt, DateTime uploadedAt)
    {
        var step = processingState switch
        {
            nameof(PdfProcessingState.Ready) => ProcessingStep.Completed,
            nameof(PdfProcessingState.Failed) => ProcessingStep.Failed,
            nameof(PdfProcessingState.Extracting) => ProcessingStep.Extracting,
            nameof(PdfProcessingState.Chunking) => ProcessingStep.Chunking,
            nameof(PdfProcessingState.Embedding) => ProcessingStep.Embedding,
            nameof(PdfProcessingState.Indexing) => ProcessingStep.Indexing,
            // Pending and Uploading: nothing has been processed yet.
            _ => ProcessingStep.Uploading,
        };

        var completed = step == ProcessingStep.Completed;
        var completedAt = completed ? processedAt : null;

        return new ProcessingProgress
        {
            CurrentStep = step,
            PercentComplete = completed ? 100 : 0,
            StartedAt = uploadedAt,
            CompletedAt = completedAt,
            ElapsedTime = completedAt is { } done && done > uploadedAt ? done - uploadedAt : TimeSpan.Zero,
            ErrorMessage = step == ProcessingStep.Failed ? processingError : null,
        };
    }
}
