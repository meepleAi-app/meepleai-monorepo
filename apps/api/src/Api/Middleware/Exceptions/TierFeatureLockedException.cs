namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a user's subscription tier does not permit the requested feature.
/// Maps to HTTP 403 Forbidden with error code "TIER_FEATURE_LOCKED".
///
/// Issue #903: SG2 — KB lifecycle with re-index.
/// Used by RebuildRaptorCommandHandler to block free-tier users from triggering RAPTOR rebuilds.
/// </summary>
public class TierFeatureLockedException : HttpException
{
    /// <summary>
    /// Gets the feature name that is locked for the user's tier.
    /// </summary>
    public string Feature { get; }

    [SetsRequiredMembers]
    public TierFeatureLockedException(string feature)
        : base(StatusCodes.Status403Forbidden, "TIER_FEATURE_LOCKED",
            $"Feature '{feature}' is not available on your current subscription tier.")
    {
        Feature = feature;
    }

    [SetsRequiredMembers]
    public TierFeatureLockedException(string feature, string message)
        : base(StatusCodes.Status403Forbidden, "TIER_FEATURE_LOCKED", message)
    {
        Feature = feature;
    }
}
