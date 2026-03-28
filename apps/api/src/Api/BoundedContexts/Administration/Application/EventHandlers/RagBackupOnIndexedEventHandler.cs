using Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Triggers an incremental RAG backup whenever a PDF document has been successfully indexed.
/// Listens to <see cref="VectorDocumentReadyIntegrationEvent"/> (published by the KnowledgeBase BC).
/// Backup failures are non-blocking: exceptions are caught and logged, never propagated.
/// </summary>
internal sealed class RagBackupOnIndexedEventHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<RagBackupOnIndexedEventHandler> _logger;

    public RagBackupOnIndexedEventHandler(
        IMediator mediator,
        ILogger<RagBackupOnIndexedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

#pragma warning disable CA1031 // Non-blocking: backup failure must not disrupt indexing
    public async Task Handle(
        VectorDocumentReadyIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "RagBackupOnIndexedEventHandler triggered: pdfDocumentId={PdfDocumentId} game={GameId} " +
            "chunks={ChunkCount} file={FileName}",
            notification.PdfDocumentId,
            notification.GameId,
            notification.ChunkCount,
            notification.FileName);

        try
        {
            var result = await _mediator
                .Send(new IncrementalRagBackupCommand(notification.PdfDocumentId), cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "IncrementalRagBackup returned failure for pdfDocumentId={PdfDocumentId}: {Error}",
                    notification.PdfDocumentId,
                    result.Error);
            }
        }
        catch (Exception ex)
        {
            // Backup failure must never interfere with indexing or other event handlers.
            _logger.LogWarning(ex,
                "IncrementalRagBackup threw an exception for pdfDocumentId={PdfDocumentId}. " +
                "Backup is skipped; indexing is unaffected.",
                notification.PdfDocumentId);
        }
    }
#pragma warning restore CA1031
}
