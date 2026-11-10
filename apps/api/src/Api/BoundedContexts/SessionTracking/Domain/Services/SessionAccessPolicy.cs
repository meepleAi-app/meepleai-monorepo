namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Who may read a session: its owner and the registered participants. The one place this rule is written
/// (#3119, #4883), so the domain (<c>Session.IsAccessibleBy</c>) and the query handlers that read the
/// persisted row directly cannot drift apart: if the rule changes, it changes here.
/// </summary>
public static class SessionAccessPolicy
{
    /// <param name="ownerUserId">The user who created the session.</param>
    /// <param name="participantUserIds">One entry per participant; a guest has no user, so <c>null</c>.</param>
    /// <param name="requestedBy">The authenticated caller, derived from the principal.</param>
    /// <remarks>
    /// Fail-closed: an empty id is not an identity (it is what an endpoint that forgot to derive the caller
    /// from the principal would pass), so it never matches, and a guest's <c>null</c> never matches either.
    /// </remarks>
    public static bool IsAccessible(Guid ownerUserId, IEnumerable<Guid?> participantUserIds, Guid requestedBy)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);

        return requestedBy != Guid.Empty
            && (ownerUserId == requestedBy || participantUserIds.Any(id => id == requestedBy));
    }
}
