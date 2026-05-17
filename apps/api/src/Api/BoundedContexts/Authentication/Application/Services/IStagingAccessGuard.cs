namespace Api.BoundedContexts.Authentication.Application.Services;

/// <summary>
/// Guards staging environment access via email allowlist (DevOps Wave 1).
/// </summary>
/// <remarks>
/// Backed by the <c>staging_allowlist</c> table (#845) — superseded the
/// <c>STAGING_ALLOWED_EMAILS</c> env-var implementation. Cached in memory with
/// a short TTL; cache is also invalidated via domain events whenever the
/// underlying table mutates (see <c>StagingAllowlistCacheInvalidator</c>).
///
/// Semantics changed in #845: empty allowlist now FAIL-CLOSED in Staging
/// (returns false). The previous "empty = open" semantics were a latent
/// landmine if the bootstrap seed failed silently.
/// See <c>docs/for-developers/operations/devops-policy.md</c> §4.
/// </remarks>
public interface IStagingAccessGuard
{
    /// <summary>
    /// Returns <c>true</c> if the email is in the (DB-backed) allowlist.
    /// Returns <c>false</c> if the allowlist is empty (fail-closed) or the email is null/whitespace.
    /// </summary>
    ValueTask<bool> IsEmailAllowedAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// True if the allowlist contains at least one entry. Used by startup warning.
    /// </summary>
    ValueTask<bool> HasNonEmptyAllowlistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the in-memory cache. Called by domain event handlers on add/remove
    /// to give admins an immediate-effect experience.
    /// </summary>
    void InvalidateCache();
}
