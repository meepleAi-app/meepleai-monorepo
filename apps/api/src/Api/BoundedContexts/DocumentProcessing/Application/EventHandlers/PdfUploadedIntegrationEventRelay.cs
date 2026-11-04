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
/// <see cref="PdfUploadedIntegrationEvent"/>. Non rilancia mai (salvo cancellazione dell'host): un'eccezione
/// farebbe rieseguire all'outbox, al retry, TUTTI gli handler fratelli di <see cref="PdfStateChangedEvent"/>;
/// la consegna è at-most-once per scelta e l'esito finisce nel contatore. Il publish ha un timeout proprio
/// perché gira dentro la transazione per-batch dell'outbox.
/// </summary>
internal sealed class PdfUploadedIntegrationEventRelay : INotificationHandler<PdfStateChangedEvent>
{
    private static readonly TimeSpan DefaultPublishTimeout = TimeSpan.FromSeconds(15);

    private readonly TimeSpan _publishTimeout;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IPdfDocumentRepository _repository;
    private readonly ILogger<PdfUploadedIntegrationEventRelay> _logger;

    public PdfUploadedIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IPdfDocumentRepository repository,
        ILogger<PdfUploadedIntegrationEventRelay> logger,
        TimeSpan? publishTimeout = null)
    {
        _publishTimeout = publishTimeout ?? DefaultPublishTimeout;
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
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_publishTimeout);
            await _publisher.PublishAsync(integrationEvent, timeout.Token).ConfigureAwait(false);
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
