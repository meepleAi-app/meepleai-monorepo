namespace Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;

/// <summary>
/// DTO for a top contributor entry on the Discover dashboard.
/// Issue #728.
///
/// Note: faqCount is intentionally absent — GameFaqEntity has no user FK,
/// making per-user FAQ count uncomputable. Contribution score uses
/// equal-weight sum: kbUploads + distinct agent definition sessions.
/// </summary>
internal sealed record TopContributorDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    int ContributionCount,
    int KbUploadCount,
    int AgentCount
);
