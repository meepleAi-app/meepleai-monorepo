namespace Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;

/// <summary>
/// DTO for a top contributor entry on the Discover dashboard.
/// Stub placeholder — full implementation in Task D (GetTopContributorsQuery handler).
/// Issue #728.
/// </summary>
internal sealed record TopContributorDto(
    Guid UserId,
    string Username,
    int ContributionCount,
    DateTime LastContributedAt
);
