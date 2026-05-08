namespace Api.BoundedContexts.Authentication.Application.Services;

/// <summary>
/// Guards staging environment access via email allowlist (DevOps wave 1).
/// </summary>
/// <remarks>
/// Reads <c>STAGING_ALLOWED_EMAILS</c> env var (CSV, case-insensitive, whitespace tolerant).
/// Empty list = open access (default safe: dev and prod don't need this gate).
/// Wave 2 will integrate with the existing <c>AccessRequest</c> BC for invite-based flow.
/// See <c>docs/for-developers/operations/devops-policy.md</c> §4.
/// </remarks>
public interface IStagingAccessGuard
{
    /// <summary>
    /// Returns <c>true</c> if email is in allowlist OR if allowlist is empty/unset.
    /// Returns <c>false</c> if allowlist is non-empty AND email not in it
    /// (or if email is null/whitespace).
    /// </summary>
    bool IsEmailAllowed(string email);

    /// <summary>
    /// True if <c>STAGING_ALLOWED_EMAILS</c> contains at least one entry.
    /// Used by startup warning to detect misconfiguration window
    /// (Staging environment with empty allowlist = silent pass-through).
    /// </summary>
    bool HasNonEmptyAllowlist { get; }
}
