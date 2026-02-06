using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Handler for updating a private game's information.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
internal sealed class UpdatePrivateGameCommandHandler : ICommandHandler<UpdatePrivateGameCommand, PrivateGameDto>
{
    private readonly IPrivateGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdatePrivateGameCommandHandler> _logger;

    public UpdatePrivateGameCommandHandler(
        IPrivateGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdatePrivateGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PrivateGameDto> Handle(UpdatePrivateGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Retrieve the private game
        var privateGame = await _repository.GetByIdAsync(command.PrivateGameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Private game with ID {command.PrivateGameId} not found");

        // Verify ownership
        if (privateGame.OwnerId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to update private game {GameId} owned by {OwnerId}",
                command.UserId,
                command.PrivateGameId,
                privateGame.OwnerId);

            throw new ForbiddenException("You can only update your own private games");
        }

        // Update game information
        privateGame.UpdateInfo(
            title: command.Title,
            minPlayers: command.MinPlayers,
            maxPlayers: command.MaxPlayers,
            yearPublished: command.YearPublished,
            description: command.Description,
            playingTimeMinutes: command.PlayingTimeMinutes,
            minAge: command.MinAge,
            complexityRating: command.ComplexityRating,
            imageUrl: command.ImageUrl);

        await _repository.UpdateAsync(privateGame, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated private game {GameId} for user {UserId}: {Title}",
            command.PrivateGameId,
            command.UserId,
            command.Title);

        return MapToDto(privateGame);
    }

    private static PrivateGameDto MapToDto(Domain.Entities.PrivateGame game)
    {
        return new PrivateGameDto(
            Id: game.Id,
            OwnerId: game.OwnerId,
            Source: game.Source.ToString(),
            BggId: game.BggId,
            Title: game.Title,
            YearPublished: game.YearPublished,
            Description: game.Description,
            MinPlayers: game.MinPlayers,
            MaxPlayers: game.MaxPlayers,
            PlayingTimeMinutes: game.PlayingTimeMinutes,
            MinAge: game.MinAge,
            ComplexityRating: game.ComplexityRating,
            ImageUrl: game.ImageUrl,
            ThumbnailUrl: game.ThumbnailUrl,
            CreatedAt: game.CreatedAt,
            UpdatedAt: game.UpdatedAt,
            BggSyncedAt: game.BggSyncedAt,
            CanProposeToCatalog: game.BggId.HasValue
        );
    }
}
