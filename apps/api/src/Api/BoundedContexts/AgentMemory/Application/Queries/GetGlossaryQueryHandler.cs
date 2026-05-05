using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Handles retrieving glossary entries for a game's memory by game and owner IDs.
/// </summary>
internal sealed class GetGlossaryQueryHandler : IQueryHandler<GetGlossaryQuery, IReadOnlyList<GlossaryEntryDto>>
{
    private readonly IGameMemoryRepository _gameMemoryRepo;

    public GetGlossaryQueryHandler(IGameMemoryRepository gameMemoryRepo)
    {
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
    }

    public async Task<IReadOnlyList<GlossaryEntryDto>> Handle(
        GetGlossaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(query.GameId, query.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (memory is null)
            return Array.Empty<GlossaryEntryDto>();

        return memory.GlossaryEntries
            .Select(e => new GlossaryEntryDto(
                e.Term,
                e.Definition,
                e.Language,
                e.Source.ToString(),
                e.AddedAt))
            .ToList();
    }
}
