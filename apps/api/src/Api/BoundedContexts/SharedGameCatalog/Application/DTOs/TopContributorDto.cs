namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO representing a top contributor for the public `/shared-games` sidebar widget.
/// Issue #593 (Wave A.3a): scope = global ranking, surfaced via
/// <c>GET /shared-games/top-contributors?limit=5</c>.
///
/// Score formula (mockup `sp3-shared-games.jsx`):
/// <c>Score = TotalSessions + TotalWins * 2</c>.
///
/// Per spec §9 decision 4, <see cref="TotalSessions"/> and <see cref="TotalWins"/>
/// count <strong>ALL</strong> sessions — including those tied to unpublished or
/// private games. Rationale: contributor reputation reflects total play activity,
/// not catalog visibility. A user whose only sessions are on draft games still
/// earns reputation while waiting for approval.
/// </summary>
public sealed record TopContributorDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    int TotalSessions,
    int TotalWins,
    int Score);
