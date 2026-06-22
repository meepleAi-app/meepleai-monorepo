namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a user has reached the maximum quota for a resource on their subscription tier.
/// Unlike <see cref="TierFeatureLockedException"/> (feature completely unavailable), this indicates
/// the feature is available on the tier but the usage quota has been consumed.
/// Maps to HTTP 402 Payment Required with error code "AGENT_SLOT_QUOTA_EXCEEDED".
///
/// Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade.
/// Used by CreateUserAgentCommandHandler when the user has reached MaxAgentsCreated.
/// </summary>
public class TierQuotaExceededException : HttpException
{
    /// <summary>
    /// Gets the quota resource name that was exceeded (e.g., "AgentSlots").
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Gets the maximum allowed quantity for the resource on the user's current tier.
    /// </summary>
    public int MaxAllowed { get; }

    [SetsRequiredMembers]
    public TierQuotaExceededException(string resource, int maxAllowed)
        : base(StatusCodes.Status402PaymentRequired, "AGENT_SLOT_QUOTA_EXCEEDED",
            $"Quota exceeded for '{resource}'. Maximum allowed: {maxAllowed}. Upgrade your subscription tier to create more.")
    {
        Resource = resource;
        MaxAllowed = maxAllowed;
    }

    [SetsRequiredMembers]
    public TierQuotaExceededException(string resource, int maxAllowed, string message)
        : base(StatusCodes.Status402PaymentRequired, "AGENT_SLOT_QUOTA_EXCEEDED", message)
    {
        Resource = resource;
        MaxAllowed = maxAllowed;
    }
}
