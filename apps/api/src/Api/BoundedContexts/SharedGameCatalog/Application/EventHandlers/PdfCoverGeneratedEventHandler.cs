using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Issue #1852 (Gap A): propagates the PDF-extracted cover key from the
/// DocumentProcessing BC onto the corresponding SharedGame so library queries
/// can compute a presigned R2 URL without a cross-BC join at query time.
/// </summary>
internal sealed class PdfCoverGeneratedEventHandler : INotificationHandler<PdfCoverGeneratedEvent>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PdfCoverGeneratedEventHandler> _logger;

    public PdfCoverGeneratedEventHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<PdfCoverGeneratedEventHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PdfCoverGeneratedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.SharedGameId is null)
        {
            _logger.LogDebug(
                "PdfCoverGenerated event for PDF {PdfDocumentId} has no SharedGameId, skipping.",
                notification.PdfDocumentId);
            return;
        }

        var sharedGameId = notification.SharedGameId.Value;
        var game = await _repository.GetByIdAsync(sharedGameId, cancellationToken).ConfigureAwait(false);

        if (game is null)
        {
            _logger.LogWarning(
                "SharedGame {SharedGameId} not found for PdfCoverGenerated event (PDF {PdfDocumentId}); game may have been deleted.",
                sharedGameId, notification.PdfDocumentId);
            return;
        }

        if (string.Equals(game.PdfCoverR2Key, notification.CoverR2Key, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "SharedGame {SharedGameId} already has PdfCoverR2Key={CoverR2Key}; skipping save.",
                sharedGameId, notification.CoverR2Key);
            return;
        }

        game.SetPdfCoverR2Key(notification.CoverR2Key);
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Propagated cover key to SharedGame {SharedGameId} from PDF {PdfDocumentId}.",
            sharedGameId, notification.PdfDocumentId);
    }
}
