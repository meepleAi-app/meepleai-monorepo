using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating an existing shared game.
/// </summary>
internal sealed class UpdateSharedGameCommandHandler : ICommandHandler<UpdateSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSharedGameCommandHandler> _logger;

    public UpdateSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(UpdateSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating shared game: {GameId}, ModifiedBy: {UserId}",
            command.GameId, command.ModifiedBy);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new InvalidOperationException($"Shared game {command.GameId} not found");
        }

        // Create rules value object if provided
        GameRules? rules = null;
        if (command.Rules is not null)
        {
            rules = GameRules.Create(command.Rules.Content, command.Rules.Language);
        }

        // Update aggregate
        game.UpdateInfo(
            command.Title,
            command.YearPublished,
            command.Description,
            command.MinPlayers,
            command.MaxPlayers,
            command.PlayingTimeMinutes,
            command.MinAge,
            command.ComplexityRating,
            command.AverageRating,
            command.ImageUrl,
            command.ThumbnailUrl,
            rules,
            command.ModifiedBy);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game updated successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
