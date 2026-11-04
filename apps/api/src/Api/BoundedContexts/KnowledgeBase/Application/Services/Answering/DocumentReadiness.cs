using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// Whether a question can be answered from the PDFs of a game, by the state of their processing.
/// </summary>
/// <remarks>
/// <para>
/// #4408: the check counted every PDF that was not <c>Ready</c> as still being processed,
/// <c>Failed</c> ones included. A single failed PDF blocked every question on the game with
/// "N of M documents are still being processed" until an admin deleted it — and the message was
/// false. A failed PDF is finished: the question is answered from the PDFs that are ready.
/// </para>
/// <para>
/// One rule for the three places that ask it (<c>AnswerPipeline</c>, <c>AskQuestionQueryHandler</c>,
/// <c>StreamDebugQaQueryHandler</c>): they were three copies of the same count, and a fix to one of
/// them would have left the others answering differently.
/// </para>
/// </remarks>
internal sealed record DocumentReadiness(int Total, int InProgress, int Failed)
{
    /// <summary>A PDF is still being processed: answering now would use part of the game's content.</summary>
    public const string ProcessingErrorCode = "DOCUMENT_PROCESSING";

    /// <summary>Every PDF of the game failed: there is nothing to answer from.</summary>
    public const string FailedErrorCode = "DOCUMENTS_FAILED";

    public static DocumentReadiness Of(IReadOnlyCollection<PdfDocument>? documents)
    {
        if (documents is null or { Count: 0 })
        {
            return new DocumentReadiness(0, 0, 0);
        }

        var failed = documents.Count(d => d.ProcessingState == PdfProcessingState.Failed);
        var inProgress = documents.Count(d =>
            d.ProcessingState != PdfProcessingState.Ready && d.ProcessingState != PdfProcessingState.Failed);
        return new DocumentReadiness(documents.Count, inProgress, failed);
    }

    /// <summary>No PDF blocks the question. A game without PDFs is not blocked here: the retrieval says it has nothing.</summary>
    public bool CanAnswer => ErrorCode is null;

    public string? ErrorCode =>
        InProgress > 0 ? ProcessingErrorCode
        : Total > 0 && Failed == Total ? FailedErrorCode
        : null;

    public string? ErrorMessage => ErrorCode switch
    {
        ProcessingErrorCode =>
            $"{InProgress} of {Total} documents are still being processed. Please wait for processing to complete before asking questions.",
        FailedErrorCode =>
            "The processing of the documents of this game failed, so there is nothing to answer from. Upload them again, or ask an admin to reindex them.",
        _ => null,
    };
}
