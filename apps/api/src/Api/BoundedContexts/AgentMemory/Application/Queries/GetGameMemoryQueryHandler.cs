using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Handles retrieving a game's memory by game and owner IDs.
/// </summary>
internal sealed class GetGameMemoryQueryHandler : IQueryHandler<GetGameMemoryQuery, GameMemoryDto?>
{
    private readonly IGameMemoryRepository _gameMemoryRepo;

    public GetGameMemoryQueryHandler(IGameMemoryRepository gameMemoryRepo)
    {
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
    }

    public async Task<GameMemoryDto?> Handle(GetGameMemoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(query.GameId, query.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        return memory != null ? MapToDto(memory) : null;
    }

    private static GameMemoryDto MapToDto(GameMemory memory) => new(
        Id: memory.Id,
        GameId: memory.GameId,
        OwnerId: memory.OwnerId,
        HouseRules: memory.HouseRules.Select(hr => new HouseRuleDto(
            Description: hr.Description,
            AddedAt: hr.AddedAt,
            Source: hr.Source.ToString()
        )).ToList(),
        Notes: memory.Notes.Select(n => new MemoryNoteDto(
            Content: n.Content,
            AddedAt: n.AddedAt
        )).ToList()
    );
}
