using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Handles retrieving all player memory records for a specific user.
/// </summary>
internal sealed class GetPlayerStatsQueryHandler : IQueryHandler<GetPlayerStatsQuery, List<PlayerMemoryDto>>
{
    private readonly IPlayerMemoryRepository _playerRepo;

    public GetPlayerStatsQueryHandler(IPlayerMemoryRepository playerRepo)
    {
        _playerRepo = playerRepo ?? throw new ArgumentNullException(nameof(playerRepo));
    }

    public async Task<List<PlayerMemoryDto>> Handle(GetPlayerStatsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memories = await _playerRepo
            .GetAllByUserIdAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return memories.Select(MapToDto).ToList();
    }

    private static PlayerMemoryDto MapToDto(PlayerMemory memory) => new(
        Id: memory.Id,
        GroupId: memory.GroupId,
        GameStats: memory.GameStats.Select(gs => new PlayerGameStatsDto(
            GameId: gs.GameId,
            Wins: gs.Wins,
            Losses: gs.Losses,
            TotalPlayed: gs.TotalPlayed,
            BestScore: gs.BestScore
        )).ToList(),
        ClaimedAt: memory.ClaimedAt
    );
}
