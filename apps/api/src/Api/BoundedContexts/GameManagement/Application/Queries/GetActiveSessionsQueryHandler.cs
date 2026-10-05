using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

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

        // #4080: an empty owner would mean "no filter" downstream, which is exactly the
        // fail-open the issue is about. Reject it here rather than querying unscoped.
        if (query.OwnerUserId == Guid.Empty)
            throw new ArgumentException("OwnerUserId is required", nameof(query));

        var sessions = await _sessionRepository.FindActiveAsync(
            ownerUserId: query.OwnerUserId,
            limit: query.Limit,
            offset: query.Offset,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        var sessionDtos = sessions.Select(s => s.ToDto()).ToList();

        // #4080: the count must be scoped too. An unscoped total would advertise pages the
        // caller cannot reach, and would still leak how many sessions exist overall.
        // Reuses the per-user count introduced for quota enforcement (#3070): same
        // CreatedByUserId + active-status filter, so count and page cannot disagree.
        var totalCount = await _sessionRepository
            .CountActiveByUserIdAsync(query.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);

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
