using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;

/// <summary>
/// Handler for retrieving a single private game.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
internal sealed class GetPrivateGameQueryHandler : IQueryHandler<GetPrivateGameQuery, PrivateGameDto>
{
    private readonly IPrivateGameRepository _repository;
    private readonly ILogger<GetPrivateGameQueryHandler> _logger;

    public GetPrivateGameQueryHandler(
        IPrivateGameRepository repository,
        ILogger<GetPrivateGameQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PrivateGameDto> Handle(GetPrivateGameQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Retrieve the private game
        var privateGame = await _repository.GetByIdAsync(query.PrivateGameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Private game with ID {query.PrivateGameId} not found");

        // Verify ownership
        if (privateGame.OwnerId != query.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to access private game {GameId} owned by {OwnerId}",
                query.UserId,
                query.PrivateGameId,
                privateGame.OwnerId);

            throw new ForbiddenException("You can only view your own private games");
        }

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
            CanProposeToCatalog: game.BggId.HasValue,
            AgentDefinitionId: game.AgentDefinitionId
        );
    }
}
