using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve FAQs for a specific game with pagination.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal record GetGameFAQsQuery(
    Guid GameId,
    int Limit = 10,
    int Offset = 0
) : IQuery<GetGameFAQsQueryResult>;

/// <summary>
/// Result containing paginated FAQs and total count.
/// </summary>
internal record GetGameFAQsQueryResult(
    IReadOnlyList<GameFAQDto> FAQs,
    int TotalCount
);
