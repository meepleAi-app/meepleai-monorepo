using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles query to get all active game sessions.
/// Issue #2755: Returns paginated response to match frontend schema.
/// </summary>
internal class GetActiveSessionsQueryHandler : IQueryHandler<GetActiveSessionsQuery, PaginatedSessionsResponseDto>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetActiveSessionsQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<PaginatedSessionsResponseDto> Handle(GetActiveSessionsQuery query, CancellationToken cancellationToken)
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
        ).ConfigureAwait(false);

        var sessionDtos = sessions.Select(s => s.ToDto()).ToList();

        // Get total count for pagination
        var totalCount = await _sessionRepository.CountActiveAsync(cancellationToken).ConfigureAwait(false);

        var limit = query.Limit ?? 20;
        var offset = query.Offset ?? 0;
        var page = (offset / limit) + 1;

        return new PaginatedSessionsResponseDto(
            Sessions: sessionDtos,
            Total: totalCount,
            Page: page,
            PageSize: limit
        );
    }
}
