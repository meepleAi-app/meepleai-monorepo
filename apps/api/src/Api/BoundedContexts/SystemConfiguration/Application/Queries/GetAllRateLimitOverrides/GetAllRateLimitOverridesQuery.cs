using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetAllRateLimitOverrides;

/// <summary>
/// Query to retrieve all rate limit overrides with pagination and optional filters.
/// Returns detailed override information including user details for admin management.
/// </summary>
public sealed record GetAllRateLimitOverridesQuery : IRequest<PagedRateLimitOverridesResult>
{
    /// <summary>
    /// Whether to include expired overrides in the results.
    /// Default is false (only active overrides).
    /// </summary>
    public bool IncludeExpired { get; init; }

    /// <summary>
    /// Page number for pagination (1-based).
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Paged result for rate limit overrides list.
/// </summary>
public sealed record PagedRateLimitOverridesResult
{
    public required IReadOnlyList<RateLimitOverrideListDto> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalPages { get; init; }
}
