using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Handles SharedGamePdfUploadedEvent by setting the HasUploadedPdf flag on the SharedGame aggregate.
/// </summary>
internal sealed class SharedGamePdfUploadedEventHandler : INotificationHandler<SharedGamePdfUploadedEvent>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SharedGamePdfUploadedEventHandler> _logger;

    public SharedGamePdfUploadedEventHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SharedGamePdfUploadedEventHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SharedGamePdfUploadedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "SharedGamePdfUploaded event received: SharedGameId={SharedGameId}. Setting HasUploadedPdf flag.",
            notification.SharedGameId);

        var game = await _repository.GetByIdAsync(notification.SharedGameId, cancellationToken).ConfigureAwait(false);

        if (game is null)
        {
            _logger.LogWarning(
                "SharedGame {SharedGameId} not found when handling SharedGamePdfUploadedEvent. " +
                "The game may have been deleted.",
                notification.SharedGameId);
            return;
        }

        game.SetHasUploadedPdf();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully set HasUploadedPdf=true on SharedGame {SharedGameId}.",
            notification.SharedGameId);
    }
}
