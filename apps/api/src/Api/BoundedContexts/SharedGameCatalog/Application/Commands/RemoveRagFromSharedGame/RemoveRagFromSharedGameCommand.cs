using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;

/// <summary>
/// Saga command that removes a PDF from a SharedGame with full multi-system cleanup:
/// 1. Handle active version promotion
/// 2. Remove SharedGameDocument link
/// 3. Delete PdfDocument (cascades VectorDoc, TextChunks, Qdrant, blob)
/// </summary>
internal record RemoveRagFromSharedGameCommand(
    Guid SharedGameId,
    Guid SharedGameDocumentId,
    Guid UserId
) : ICommand<RemoveRagFromSharedGameResult>;

/// <summary>
/// Result of the removal saga. Cleanup flags indicate best-effort external system status.
/// </summary>
internal record RemoveRagFromSharedGameResult(
    Guid RemovedSharedGameDocumentId,
    Guid RemovedPdfDocumentId,
    bool QdrantCleanupSucceeded,
    bool BlobCleanupSucceeded
);
