using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.EventHandlers;

/// <summary>
/// Creates a default Toolkit when a game is added to a user's library (BR-01).
/// The default Toolkit has all 6 widgets enabled and is shared across all users
/// who have the same game in their library (OwnerUserId = null, IsDefault = true).
/// Issue #5146 — Epic B3.
/// </summary>
internal sealed class CreateDefaultToolkitWhenGameAddedHandler
    : INotificationHandler<GameAddedToLibraryEvent>
{
    private readonly IToolkitRepository _toolkitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateDefaultToolkitWhenGameAddedHandler> _logger;

    public CreateDefaultToolkitWhenGameAddedHandler(
        IToolkitRepository toolkitRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateDefaultToolkitWhenGameAddedHandler> logger)
    {
        _toolkitRepository = toolkitRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(GameAddedToLibraryEvent notification, CancellationToken cancellationToken)
    {
        // Idempotency check: only create a default toolkit once per game
        var alreadyExists = await _toolkitRepository
            .ExistsDefaultAsync(notification.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyExists)
        {
            _logger.LogDebug(
                "Default toolkit already exists for game {GameId}, skipping creation",
                notification.GameId);
            return;
        }

        try
        {
            var toolkit = Toolkit.CreateDefault(notification.GameId);
            await _toolkitRepository.AddAsync(toolkit, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created default Toolkit {ToolkitId} for game {GameId} (triggered by user {UserId} adding game to library)",
                toolkit.Id,
                notification.GameId,
                notification.UserId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            // Log but don't throw — toolkit creation failure shouldn't block library add
            _logger.LogError(
                ex,
                "Failed to create default Toolkit for game {GameId}. Error: {Message}",
                notification.GameId,
                ex.Message);
        }
#pragma warning restore CA1031
    }
}
