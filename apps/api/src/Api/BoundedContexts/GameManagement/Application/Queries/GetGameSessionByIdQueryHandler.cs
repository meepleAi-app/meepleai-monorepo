using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles get game session by ID query. On this single-session path (unlike the
/// list/history path) the DTO is enriched with the game slug/name (#3022) and the
/// polymorphic score + score-aligned players for the summary flavor.
/// </summary>
internal class GetGameSessionByIdQueryHandler : IQueryHandler<GetGameSessionByIdQuery, GameSessionDto?>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IGameCoreDataProvider _gameCoreData;
    private readonly IHistorySessionScoreProvider _scoreProvider;
    private readonly ILiveSessionLinkReader _liveLinks;

    public GetGameSessionByIdQueryHandler(
        IGameSessionRepository sessionRepository,
        IGameCoreDataProvider gameCoreData,
        IHistorySessionScoreProvider scoreProvider,
        ILiveSessionLinkReader liveLinks)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
        _scoreProvider = scoreProvider ?? throw new ArgumentNullException(nameof(scoreProvider));
        _liveLinks = liveLinks ?? throw new ArgumentNullException(nameof(liveLinks));
    }

    public async Task<GameSessionDto?> Handle(GetGameSessionByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null) return null;

        var coreData = await _gameCoreData
            .GetCoreDataAsync(GameRef.Shared(session.GameId), cancellationToken).ConfigureAwait(false);
        var gameName = coreData?.Title;
        var gameSlug = gameName is null ? null : Slugifier.Slugify(gameName);

        var scoreboard = await _scoreProvider
            .GetScoreboardAsync(session.Id, cancellationToken).ConfigureAwait(false);

        // #4156: the session pages address the live session this GameSession is the companion of.
        var liveLinks = await _liveLinks
            .GetLiveSessionIdsAsync([session.Id], cancellationToken).ConfigureAwait(false);
        var dto = MapToDto(session, gameSlug, gameName, scoreboard);
        return liveLinks.TryGetValue(session.Id, out var liveSessionId) ? dto with { LiveSessionId = liveSessionId } : dto;
    }

    private static GameSessionDto MapToDto(
        GameSession session, string? gameSlug, string? gameName, SessionScoreboard? scoreboard)
    {
        var playerDtos = session.Players.Select(p => new SessionPlayerDto(
            PlayerName: p.PlayerName,
            PlayerOrder: p.PlayerOrder,
            Color: p.Color
        )).ToList();

        var scorePlayers = scoreboard?.Players
            .Select(sp => new ScorePlayerDto(sp.Id, sp.DisplayName, sp.Color))
            .ToList();

        return new GameSessionDto(
            Id: session.Id,
            GameId: session.GameId,
            Status: session.Status.Value,
            StartedAt: session.StartedAt,
            CompletedAt: session.CompletedAt,
            PlayerCount: session.PlayerCount,
            Players: playerDtos,
            WinnerName: session.WinnerName,
            Notes: session.Notes,
            DurationMinutes: (int)session.Duration.TotalMinutes,
            ScoringType: scoreboard?.ScoringType,
            ScoreData: scoreboard?.ScoreData,
            GameSlug: gameSlug,
            GameName: gameName,
            ScorePlayers: scorePlayers
        );
    }
}
