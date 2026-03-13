namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Detects the user's geographic region from request context.
/// Issue #27: Multi-region preparation — region hint for routing decisions.
/// Currently used to populate LlmRoutingDecision.UserRegion (logged, not acted upon).
/// </summary>
internal interface IUserRegionDetector
{
    /// <summary>
    /// Returns a region code derived from the current HTTP request context,
    /// or null if region cannot be determined.
    /// Examples: "en-US", "it-IT", "de-DE".
    /// </summary>
    string? DetectRegion();
}
