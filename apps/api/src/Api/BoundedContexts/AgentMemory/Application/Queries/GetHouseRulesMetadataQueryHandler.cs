using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Services;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Handles <see cref="GetHouseRulesMetadataQuery"/> by aggregating the 4 ConnectionBar pip
/// counts (agent · game · session · kb) for the SP6 §F drawer (issue #2492).
///
/// Semantic counts:
/// - <c>AgentCount</c>: number of house rules persisted for the (gameId, ownerId) pair
/// - <c>GameCount</c>: always 1 in current scope; future-proof for multi-game rules
/// - <c>SessionCount</c>: 0 today; requires a future <c>session_house_rule_applications</c>
///   tracking table (out of scope here)
/// - <c>KbCount</c>: number of indexed VectorDocuments associated with the game,
///   read cross-BC via <see cref="IKbChunkCountReader"/>
/// </summary>
internal sealed class GetHouseRulesMetadataQueryHandler
    : IQueryHandler<GetHouseRulesMetadataQuery, HouseRulesMetadataDto>
{
    private readonly IGameMemoryRepository _gameMemoryRepo;
    private readonly IKbChunkCountReader _kbChunkCountReader;

    public GetHouseRulesMetadataQueryHandler(
        IGameMemoryRepository gameMemoryRepo,
        IKbChunkCountReader kbChunkCountReader)
    {
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
        _kbChunkCountReader = kbChunkCountReader ?? throw new ArgumentNullException(nameof(kbChunkCountReader));
    }

    public async Task<HouseRulesMetadataDto> Handle(
        GetHouseRulesMetadataQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(query.GameId, query.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        var kbCount = await _kbChunkCountReader
            .CountByGameAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        var agentCount = memory?.HouseRules.Count ?? 0;

        return new HouseRulesMetadataDto(
            AgentCount: agentCount,
            GameCount: 1,
            SessionCount: 0,
            KbCount: kbCount);
    }
}
