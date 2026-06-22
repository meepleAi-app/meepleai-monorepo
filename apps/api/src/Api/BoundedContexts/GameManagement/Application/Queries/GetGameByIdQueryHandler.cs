using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles get game by ID query.
/// Issue #1320 (P2c): Migrated from IGameRepository to IGameCoreDataProvider.
/// Resolves against SharedGame via IGameCoreDataProvider.
/// Issue #2243 (epic #2242) Block C: also looks up SharedGame.HasKnowledgeBase
/// so the GameDto carries the "agente pronto" flag without an extra round-trip.
/// </summary>
internal class GetGameByIdQueryHandler : IQueryHandler<GetGameByIdQuery, GameDto?>
{
    private readonly IGameCoreDataProvider _gameCoreData;
    private readonly MeepleAiDbContext _db;

    public GetGameByIdQueryHandler(IGameCoreDataProvider gameCoreData, MeepleAiDbContext db)
    {
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
        _db = db ?? throw new ArgumentNullException(nameof(db));
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

        if (coreData is null)
        {
            return null;
        }

        // Issue #2243 Block C: single projection-only read for the KB flag.
        // We intentionally keep this outside GameCoreData (a domain VO shared with PrivateGame)
        // so SharedGame-specific projections don't leak into the value object.
        var hasKnowledgeBase = await _db.SharedGames
            .AsNoTracking()
            .Where(g => g.Id == query.GameId)
            .Select(g => g.HasKnowledgeBase)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return MapToDto(query.GameId, coreData, hasKnowledgeBase);
    }

    private static GameDto MapToDto(Guid id, GameCoreData coreData, bool hasKnowledgeBase)
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
            ImageUrl: coreData.ImageUrl,
            HasKnowledgeBase: hasKnowledgeBase
        );
    }
}
