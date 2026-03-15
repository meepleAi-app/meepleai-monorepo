using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Handles PdfReadyForProcessingEvent by dispatching indexing to the DocumentProcessing BC.
/// Currently logs the event; will send an indexing command via MediatR once the pipeline is wired.
/// </summary>
internal sealed class PdfReadyForProcessingEventHandler : INotificationHandler<PdfReadyForProcessingEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<PdfReadyForProcessingEventHandler> _logger;

    public PdfReadyForProcessingEventHandler(
        ISender sender,
        ILogger<PdfReadyForProcessingEventHandler> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(PdfReadyForProcessingEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "PdfReadyForProcessing event received: PdfDocumentId={PdfDocumentId}, SharedGameId={SharedGameId}, UserId={UserId}. " +
            "Dispatching to DocumentProcessing pipeline.",
            notification.PdfDocumentId,
            notification.SharedGameId,
            notification.UserId);

        _ = _sender;

        return Task.CompletedTask;
    }
}
