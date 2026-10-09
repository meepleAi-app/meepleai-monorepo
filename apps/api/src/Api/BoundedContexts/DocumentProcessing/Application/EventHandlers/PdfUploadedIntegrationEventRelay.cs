using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Traduce la transizione Pending → Uploading di <see cref="PdfStateChangedEvent"/> nell'integration event
/// <see cref="PdfUploadedIntegrationEvent"/>. Mai un'eccezione verso l'upload: il bus giù non deve far fallire
/// la richiesta dell'utente (Review Focus 5); l'esito finisce nel contatore.
/// </summary>
internal sealed class PdfUploadedIntegrationEventRelay : INotificationHandler<PdfStateChangedEvent>
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IPdfDocumentRepository _repository;
    private readonly ILogger<PdfUploadedIntegrationEventRelay> _logger;

    public PdfUploadedIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IPdfDocumentRepository repository,
        ILogger<PdfUploadedIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(PdfStateChangedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.PreviousState != PdfProcessingState.Pending || notification.NewState != PdfProcessingState.Uploading)
        {
            return;
        }

        try
        {
            var document = await _repository.GetByIdAsync(notification.PdfDocumentId, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                _logger.LogWarning("PdfUploaded not published: document {PdfId} not found", notification.PdfDocumentId);
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound);
                return;
            }

            var integrationEvent = new PdfUploadedIntegrationEvent(document.Id, document.GameId, document.SharedGameId, document.FilePath);
            await _publisher.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
            MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Published);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Confine: la pubblicazione è best-effort, l'upload non deve fallire
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfUploaded publish failed for {PdfId}", notification.PdfDocumentId);
            MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.PublishFailed);
        }
#pragma warning restore CA1031
    }
}
