using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for share request information in user dashboard.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
public sealed record UserShareRequestDto(
    Guid Id,
    Guid SourceGameId,
    string GameTitle,
    string? GameThumbnailUrl,
    ShareRequestStatus Status,
    ContributionType ContributionType,
    string? UserNotes,
    string? AdminFeedback,
    int AttachedDocumentCount,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    Guid? ResultingSharedGameId);

/// <summary>
/// DTO for user contribution information.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
public sealed record UserContributionDto(
    Guid ContributorId,
    Guid SharedGameId,
    string GameTitle,
    string? GameThumbnailUrl,
    bool IsPrimaryContributor,
    int ContributionCount,
    DateTime FirstContributionAt,
    DateTime LastContributionAt,
    IReadOnlyList<ContributionRecordDto> Contributions);

/// <summary>
/// DTO for individual contribution record.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
public sealed record ContributionRecordDto(
    Guid Id,
    ContributionRecordType Type,
    string Description,
    int DocumentCount,
    DateTime ContributedAt);

/// <summary>
/// DTO for user contribution statistics.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
public sealed record UserContributionStatsDto(
    Guid UserId,
    int TotalShareRequests,
    int PendingRequests,
    int ApprovedRequests,
    int RejectedRequests,
    int TotalContributions,
    int GamesContributed,
    int DocumentsContributed,
    int PrimaryContributions,
    decimal ApprovalRate,
    int ConsecutiveApprovalsWithoutChanges,
    DateTime? FirstContributionAt,
    DateTime? LastContributionAt,
    RateLimitStatusDto RateLimitStatus);

/// <summary>
/// DTO for rate limit status information.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
public sealed record RateLimitStatusDto(
    int CurrentPendingCount,
    int MaxPendingAllowed,
    int CurrentMonthlyCount,
    int MaxMonthlyAllowed,
    bool IsInCooldown,
    DateTime? CooldownEndsAt,
    DateTime MonthResetAt);
