using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles query to get session history with filters.
/// </summary>
public class GetSessionHistoryQueryHandler : IQueryHandler<GetSessionHistoryQuery, List<GameSessionDto>>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetSessionHistoryQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
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
        );

        return sessions.Select(s => s.ToDto()).ToList();
    }
}