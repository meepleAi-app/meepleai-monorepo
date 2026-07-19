using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles query to get session history with filters.
/// </summary>
internal class GetSessionHistoryQueryHandler : IQueryHandler<GetSessionHistoryQuery, List<GameSessionDto>>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IHistorySessionScoreProvider _scoreProvider;

    public GetSessionHistoryQueryHandler(
        IGameSessionRepository sessionRepository,
        IHistorySessionScoreProvider scoreProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _scoreProvider = scoreProvider ?? throw new ArgumentNullException(nameof(scoreProvider));
    }

    public async Task<List<GameSessionDto>> Handle(GetSessionHistoryQuery query, CancellationToken cancellationToken)
    {
        // Validate pagination parameters
        if (query.Limit.HasValue && query.Limit.Value < 0)
            throw new ArgumentException("Limit must be non-negative", nameof(query));
        if (query.Limit.HasValue && query.Limit.Value > 1000)
            throw new ArgumentException("Limit cannot exceed 1000", nameof(query));
        if (query.Offset.HasValue && query.Offset.Value < 0)
            throw new ArgumentException("Offset must be non-negative", nameof(query));

        var sessions = await _sessionRepository.FindHistoryAsync(
            gameId: query.GameId,
            startDate: query.StartDate,
            endDate: query.EndDate,
            limit: query.Limit,
            offset: query.Offset,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        var dtos = sessions.Select(s => s.ToDto()).ToList();
        if (dtos.Count == 0)
            return dtos;

        // Enrich with the polymorphic score, which lives in the SessionTracking
        // context and is bridged only via LiveGameSession (#3080). Sessions with no
        // resolvable score are left with null ScoringType/ScoreData.
        var scores = await _scoreProvider
            .GetScoresAsync(dtos.Select(d => d.Id).ToList(), cancellationToken)
            .ConfigureAwait(false);
        if (scores.Count == 0)
            return dtos;

        return dtos
            .Select(d => scores.TryGetValue(d.Id, out var score)
                ? d with { ScoringType = score.ScoringType, ScoreData = score.ScoreData }
                : d)
            .ToList();
    }
}
