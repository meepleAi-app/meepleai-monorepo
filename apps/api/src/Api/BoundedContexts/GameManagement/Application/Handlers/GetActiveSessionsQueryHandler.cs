using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles query to get all active game sessions.
/// </summary>
public class GetActiveSessionsQueryHandler : IQueryHandler<GetActiveSessionsQuery, List<GameSessionDto>>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetActiveSessionsQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<List<GameSessionDto>> Handle(GetActiveSessionsQuery query, CancellationToken cancellationToken)
    {
        // Validate pagination parameters
        if (query.Limit.HasValue && query.Limit.Value < 0)
            throw new ArgumentException("Limit must be non-negative", nameof(query));
        if (query.Limit.HasValue && query.Limit.Value > 1000)
            throw new ArgumentException("Limit cannot exceed 1000", nameof(query));
        if (query.Offset.HasValue && query.Offset.Value < 0)
            throw new ArgumentException("Offset must be non-negative", nameof(query));

        var sessions = await _sessionRepository.FindActiveAsync(
            limit: query.Limit,
            offset: query.Offset,
            cancellationToken: cancellationToken
        );

        return sessions.Select(s => s.ToDto()).ToList();
    }
}