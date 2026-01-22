using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Enum for sorting share requests in admin dashboard.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public enum ShareRequestSortField
{
    CreatedAt,
    GameTitle,
    ContributorName,
    Status
}

/// <summary>
/// Enum for sorting direction.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Enum for share request history actions.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public enum ShareRequestHistoryAction
{
    Created,
    DocumentsUpdated,
    ReviewStarted,
    ReviewReleased,
    ChangesRequested,
    Resubmitted,
    Approved,
    Rejected,
    Withdrawn
}

/// <summary>
/// DTO for share request in admin pending queue.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record AdminShareRequestDto(
    Guid Id,
    ShareRequestStatus Status,
    ContributionType ContributionType,

    // Game preview
    Guid SourceGameId,
    string GameTitle,
    string? GameThumbnailUrl,
    int? BggId,

    // Contributor info
    Guid UserId,
    string UserName,
    string? UserAvatarUrl,
    int UserTotalContributions,

    // Request details
    string? UserNotes,
    int AttachedDocumentCount,
    DateTime CreatedAt,

    // Lock info
    bool IsInReview,
    Guid? ReviewingAdminId,
    string? ReviewingAdminName,
    DateTime? ReviewStartedAt,

    // For additional content
    Guid? TargetSharedGameId,
    string? TargetSharedGameTitle)
{
    /// <summary>
    /// Calculated waiting time since request creation.
    /// </summary>
    public TimeSpan WaitingTime => DateTime.UtcNow - CreatedAt;
}

/// <summary>
/// DTO for detailed share request view in admin dashboard.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record ShareRequestDetailsDto(
    Guid Id,
    ShareRequestStatus Status,
    ContributionType ContributionType,

    // Full game details
    GameDetailsDto SourceGame,

    // If additional content, show existing game for comparison
    GameDetailsDto? TargetSharedGame,

    // Contributor details
    ContributorProfileDto Contributor,

    // Request content
    string? UserNotes,
    IReadOnlyList<DocumentPreviewDto> AttachedDocuments,

    // History
    IReadOnlyList<ShareRequestHistoryEntryDto> History,

    // Lock status
    LockStatusDto LockStatus,

    // Timestamps
    DateTime CreatedAt,
    DateTime? ResolvedAt);

/// <summary>
/// DTO for game details in share request preview.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record GameDetailsDto(
    Guid Id,
    string Title,
    string? Description,
    string? ThumbnailUrl,
    int? BggId,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTime,
    decimal? Complexity,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Mechanisms);

/// <summary>
/// DTO for document preview in share request details.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record DocumentPreviewDto(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long FileSize,
    string? PreviewUrl,
    int? PageCount);

/// <summary>
/// DTO for contributor profile in share request details.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record ContributorProfileDto(
    Guid UserId,
    string UserName,
    string? AvatarUrl,
    DateTime JoinedAt,
    int TotalContributions,
    int ApprovedContributions,
    decimal ApprovalRate,
    IReadOnlyList<BadgeDto> Badges);

/// <summary>
/// DTO for badge information.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record BadgeDto(
    Guid Id,
    string Name,
    string? IconUrl,
    DateTime AwardedAt);

/// <summary>
/// DTO for review lock status.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record LockStatusDto(
    bool IsLocked,
    bool IsLockedByCurrentAdmin,
    Guid? LockedByAdminId,
    string? LockedByAdminName,
    DateTime? LockExpiresAt);

/// <summary>
/// DTO for share request history entry.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
public sealed record ShareRequestHistoryEntryDto(
    DateTime Timestamp,
    ShareRequestHistoryAction Action,
    Guid? ActorId,
    string? ActorName,
    string? Details);
