using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles query to get all sessions for a specific game with optional pagination.
/// </summary>
internal class GetGameSessionsQueryHandler : IQueryHandler<GetGameSessionsQuery, List<GameSessionDto>>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetGameSessionsQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<List<GameSessionDto>> Handle(GetGameSessionsQuery query, CancellationToken cancellationToken)
    {
        // Validate pagination parameters
        if (query.PageSize.HasValue && query.PageSize.Value < 1)
            throw new ArgumentException("Page size must be positive", nameof(query));
        if (query.PageSize.HasValue && query.PageSize.Value > 1000)
            throw new ArgumentException("Page size cannot exceed 1000", nameof(query));
        if (query.PageNumber.HasValue && query.PageNumber.Value < 1)
            throw new ArgumentException("Page number must be positive (1-based)", nameof(query));

        // Get all sessions for the game
        var sessions = await _sessionRepository.FindByGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);

        // Apply pagination if requested (in-memory for MVP)
        var sessionsList = sessions.AsEnumerable();

        if (query.PageNumber.HasValue && query.PageSize.HasValue)
        {
            var skip = (query.PageNumber.Value - 1) * query.PageSize.Value;
            sessionsList = sessionsList.Skip(skip).Take(query.PageSize.Value);
        }

        return sessionsList.Select(s => s.ToDto()).ToList();
    }
}
