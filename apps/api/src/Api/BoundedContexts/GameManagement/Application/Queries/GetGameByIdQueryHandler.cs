using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles get game by ID query.
/// Issue #1320 (P2c): Migrated from IGameRepository to IGameCoreDataProvider.
/// Resolves against SharedGame via IGameCoreDataProvider.
/// </summary>
internal class GetGameByIdQueryHandler : IQueryHandler<GetGameByIdQuery, GameDto?>
{
    private readonly IGameCoreDataProvider _gameCoreData;

    public GetGameByIdQueryHandler(IGameCoreDataProvider gameCoreData)
    {
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
    }

    public async Task<GameDto?> Handle(GetGameByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.GameId == Guid.Empty)
        {
            return null;
        }

        var coreData = await _gameCoreData
            .GetCoreDataAsync(GameRef.Shared(query.GameId), cancellationToken)
            .ConfigureAwait(false);

        return coreData != null ? MapToDto(query.GameId, coreData) : null;
    }

    private static GameDto MapToDto(Guid id, GameCoreData coreData)
    {
        return new GameDto(
            Id: id,
            Title: coreData.Title,
            Publisher: null,
            YearPublished: coreData.YearPublished,
            MinPlayers: coreData.MinPlayers,
            MaxPlayers: coreData.MaxPlayers,
            MinPlayTimeMinutes: coreData.PlayingTimeMinutes,
            MaxPlayTimeMinutes: coreData.PlayingTimeMinutes,
            BggId: coreData.BggId,
            CreatedAt: DateTime.UtcNow,
            ImageUrl: coreData.ImageUrl
        );
    }
}
