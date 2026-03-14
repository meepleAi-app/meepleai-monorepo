using Api.BoundedContexts.UserLibrary.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Handles PrivatePdfRemovedEvent to cleanup vectors from private_rules collection.
/// Issue #3651: Ensures vector data isolation is maintained when private PDFs are removed.
/// NOTE: Vector store (Qdrant) has been removed — this handler is now a no-op.
/// </summary>
internal sealed class PrivatePdfRemovedEventHandler : INotificationHandler<PrivatePdfRemovedEvent>
{
    private readonly ILogger<PrivatePdfRemovedEventHandler> _logger;

    public PrivatePdfRemovedEventHandler(
        ILogger<PrivatePdfRemovedEventHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(PrivatePdfRemovedEvent notification, CancellationToken cancellationToken)
    {
        // Vector store (Qdrant) has been removed — vector cleanup is no longer needed.
        _logger.LogInformation(
            "PrivatePdfRemovedEvent received: User={UserId}, PDF={PdfId}, LibraryEntry={EntryId} (vector cleanup skipped — Qdrant removed)",
            notification.UserId, notification.PdfDocumentId, notification.LibraryEntryId);

        return Task.CompletedTask;
    }
}
