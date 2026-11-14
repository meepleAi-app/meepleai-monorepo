namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Thrown when a user already has as many private games as their tier allows
/// (<see cref="Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects.TierLimits.MaxPrivateGames"/>).
/// Maps to HTTP 402 Payment Required with error code "PRIVATE_GAME_QUOTA_EXCEEDED".
///
/// Issue #4978: the Bruno API smoke found that POST /private-games never checked the limit, so a free
/// user created private games without end. The limit counts the private games the user has (not deleted);
/// <see cref="TierQuotaExceededException"/> is the agents' variant and carries their error code.
/// </summary>
public class PrivateGameQuotaExceededException : HttpException
{
    /// <summary>
    /// Gets the maximum number of private games on the user's current tier.
    /// </summary>
    public int MaxAllowed { get; }

    [SetsRequiredMembers]
    public PrivateGameQuotaExceededException(int maxAllowed)
        : base(StatusCodes.Status402PaymentRequired, "PRIVATE_GAME_QUOTA_EXCEEDED",
            $"You already have {maxAllowed} private games, the maximum on your current plan. Delete one or upgrade your plan to add more.")
    {
        MaxAllowed = maxAllowed;
    }
}
