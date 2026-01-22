using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO representing an active review being conducted by an admin.
/// </summary>
public sealed record ActiveReviewDto(
    Guid ShareRequestId,
    Guid SourceGameId,
    string GameTitle,
    Guid ContributorId,
    string ContributorName,
    DateTime ReviewStartedAt,
    DateTime ReviewLockExpiresAt,
    ShareRequestStatus Status)
{
    /// <summary>
    /// Gets the duration of the review so far.
    /// </summary>
    public TimeSpan ReviewDuration => DateTime.UtcNow - ReviewStartedAt;

    /// <summary>
    /// Gets the remaining time on the review lock.
    /// </summary>
    public TimeSpan TimeRemaining
    {
        get
        {
            var remaining = ReviewLockExpiresAt - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Gets whether the review lock has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ReviewLockExpiresAt;
}
