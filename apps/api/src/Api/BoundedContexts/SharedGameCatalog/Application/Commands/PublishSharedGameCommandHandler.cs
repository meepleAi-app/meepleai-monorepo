using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for publishing a shared game.
/// </summary>
internal sealed class PublishSharedGameCommandHandler : ICommandHandler<PublishSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PublishSharedGameCommandHandler> _logger;

    public PublishSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<PublishSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(PublishSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Publishing shared game: {GameId}, PublishedBy: {UserId}",
            command.GameId, command.PublishedBy);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {command.GameId} not found");

        // Call domain method (validates status and raises event)
        // Using obsolete method for backward compatibility until all clients migrate to approval workflow
#pragma warning disable CS0618 // Type or member is obsolete
        game.Publish(command.PublishedBy);
#pragma warning restore CS0618 // Type or member is obsolete

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game published successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
