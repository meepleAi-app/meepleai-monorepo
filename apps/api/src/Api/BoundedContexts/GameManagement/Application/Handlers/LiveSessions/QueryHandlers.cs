using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Handles retrieving full live session details by ID.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal class GetLiveSessionQueryHandler : IQueryHandler<GetLiveSessionQuery, LiveSessionDto>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetLiveSessionQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<LiveSessionDto> Handle(GetLiveSessionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", query.SessionId.ToString());

        return MapToDto(session);
    }

    internal static LiveSessionDto MapToDto(LiveGameSession session)
    {
        var currentTurnPlayerId = session.GetCurrentTurnPlayerId();

        return new LiveSessionDto(
            session.Id,
            session.SessionCode,
            session.GameId,
            session.GameName,
            session.CreatedByUserId,
            session.Status,
            session.Visibility,
            session.GroupId,
            session.CreatedAt,
            session.StartedAt,
            session.PausedAt,
            session.CompletedAt,
            session.UpdatedAt,
            session.LastSavedAt,
            session.CurrentTurnIndex,
            currentTurnPlayerId == Guid.Empty ? null : currentTurnPlayerId,
            session.AgentMode,
            session.ChatSessionId,
            session.Notes,
            session.Players.Select(p => new LiveSessionPlayerDto(
                p.Id,
                p.UserId,
                p.DisplayName,
                p.AvatarUrl,
                p.Color,
                p.Role,
                p.TeamId,
                p.TotalScore,
                p.CurrentRank,
                p.JoinedAt,
                p.IsActive
            )).ToList(),
            session.Teams.Select(t => new LiveSessionTeamDto(
                t.Id,
                t.Name,
                t.Color,
                t.PlayerIds,
                t.TeamScore,
                t.CurrentRank
            )).ToList(),
            session.RoundScores.Select(s => new LiveSessionRoundScoreDto(
                s.PlayerId,
                s.Round,
                s.Dimension,
                s.Value,
                s.Unit,
                s.RecordedAt
            )).ToList(),
            new LiveSessionScoringConfigDto(
                session.ScoringConfig.EnabledDimensions,
                session.ScoringConfig.DimensionUnits
            )
        );
    }
}

/// <summary>
/// Handles retrieving a live session by its join code.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal class GetLiveSessionByCodeQueryHandler : IQueryHandler<GetLiveSessionByCodeQuery, LiveSessionDto>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetLiveSessionByCodeQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<LiveSessionDto> Handle(GetLiveSessionByCodeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository.GetByCodeAsync(query.SessionCode, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", $"code:{query.SessionCode}");

        return GetLiveSessionQueryHandler.MapToDto(session);
    }
}

/// <summary>
/// Handles retrieving all active sessions for a user.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal class GetUserActiveSessionsQueryHandler : IQueryHandler<GetUserActiveSessionsQuery, IReadOnlyList<LiveSessionSummaryDto>>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetUserActiveSessionsQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<IReadOnlyList<LiveSessionSummaryDto>> Handle(GetUserActiveSessionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sessions = await _sessionRepository.GetActiveByUserIdAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return sessions.Select(s => new LiveSessionSummaryDto(
            s.Id,
            s.SessionCode,
            s.GameName,
            s.Status,
            s.PlayerCount,
            s.CurrentTurnIndex,
            s.CreatedAt,
            s.UpdatedAt,
            s.LastSavedAt
        )).ToList();
    }
}

/// <summary>
/// Handles retrieving scores for a live session.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal class GetSessionScoresQueryHandler : IQueryHandler<GetSessionScoresQuery, IReadOnlyList<LiveSessionRoundScoreDto>>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetSessionScoresQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<IReadOnlyList<LiveSessionRoundScoreDto>> Handle(GetSessionScoresQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", query.SessionId.ToString());

        return session.RoundScores.Select(s => new LiveSessionRoundScoreDto(
            s.PlayerId,
            s.Round,
            s.Dimension,
            s.Value,
            s.Unit,
            s.RecordedAt
        )).ToList();
    }
}

/// <summary>
/// Handles retrieving players in a live session.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal class GetSessionPlayersQueryHandler : IQueryHandler<GetSessionPlayersQuery, IReadOnlyList<LiveSessionPlayerDto>>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetSessionPlayersQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<IReadOnlyList<LiveSessionPlayerDto>> Handle(GetSessionPlayersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", query.SessionId.ToString());

        return session.Players.Select(p => new LiveSessionPlayerDto(
            p.Id,
            p.UserId,
            p.DisplayName,
            p.AvatarUrl,
            p.Color,
            p.Role,
            p.TeamId,
            p.TotalScore,
            p.CurrentRank,
            p.JoinedAt,
            p.IsActive
        )).ToList();
    }
}
