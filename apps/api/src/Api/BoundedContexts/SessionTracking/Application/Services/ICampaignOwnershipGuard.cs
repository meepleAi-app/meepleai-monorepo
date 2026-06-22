namespace Api.BoundedContexts.SessionTracking.Application.Services;

/// <summary>
/// Verifies that a campaign is owned by the specified user. Used both as a pre-flight
/// gate in SSE endpoints (before headers are flushed) and inside command/query handlers
/// to enforce ownership invariants.
/// </summary>
/// <remarks>
/// Implementations MUST be request-scoped and memoize positive verification results inside
/// the current <see cref="Microsoft.AspNetCore.Http.HttpContext"/> so that pre-flight +
/// handler chained calls do not cause a double DB roundtrip in the happy path.
/// </remarks>
internal interface ICampaignOwnershipGuard
{
    /// <summary>
    /// Throws <see cref="Api.Middleware.Exceptions.ForbiddenException"/> if the campaign
    /// exists but is owned by a different user. Throws
    /// <see cref="Api.Middleware.Exceptions.NotFoundException"/> if the campaign does not
    /// exist. Throws <see cref="System.TimeoutException"/> if the DB lookup exceeds the
    /// configured preflight timeout (default 2 seconds).
    /// </summary>
    Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken);
}
