namespace Api.BoundedContexts.SecurityAudit.Application.Services;

/// <summary>
/// I10 (auth security fixes): canonical event-type constants for the
/// <c>security_audit_logs</c> table. Strings are hierarchical (dot-separated)
/// so audit dashboards can filter by domain prefix
/// (e.g. <c>auth.*</c> for all authentication events).
///
/// Adding a new event type:
///   1. Add a const here with the canonical hierarchical name.
///   2. Document the metadata shape (JSON keys) in the const's XML doc.
///   3. Call <see cref="IAuditLogger.LogAsync"/> from the appropriate
///      handler — never inline-string the event type at the call site.
/// </summary>
public static class AuditEventType
{
    // ── Authentication (login flows) ────────────────────────────────────

    /// <summary>
    /// Successful login (post-2FA if 2FA is enabled). Metadata: none.
    /// ActorUserId = the user who logged in.
    /// </summary>
    public const string LoginSuccess = "auth.login.success";

    /// <summary>
    /// Failed login attempt (wrong password, unknown email, suspended
    /// account, etc.). Metadata JSON keys:
    ///   - <c>emailMasked</c>: masked attempted email (never the raw value).
    ///   - <c>reason</c>: short cause label (e.g. "invalid_credentials",
    ///     "account_unavailable", "lockout_active").
    /// ActorUserId = null (the email may not even resolve to a user).
    /// </summary>
    public const string LoginFailure = "auth.login.failure";

    /// <summary>
    /// Account locked after exceeding the failed-login threshold.
    /// Metadata JSON keys:
    ///   - <c>attemptCount</c>: int.
    ///   - <c>lockoutUntilUtc</c>: ISO-8601.
    /// ActorUserId = the user being locked.
    /// </summary>
    public const string AccountLocked = "auth.account.locked";

    // ── Authentication (credential mutations) ───────────────────────────

    /// <summary>
    /// User changed their own password.
    /// ActorUserId = TargetUserId = the user.
    /// </summary>
    public const string PasswordChanged = "auth.password.changed";

    /// <summary>
    /// Admin or self-service password reset completed.
    /// ActorUserId = whoever drove the reset (admin or user themselves);
    /// TargetUserId = the user whose password was reset.
    /// </summary>
    public const string PasswordReset = "auth.password.reset";

    // ── Authentication (OAuth) ──────────────────────────────────────────

    /// <summary>
    /// OAuth account linked to an existing user.
    /// Metadata JSON: <c>{ "provider": "google" }</c>.
    /// </summary>
    public const string OAuthLinked = "auth.oauth.linked";

    /// <summary>
    /// OAuth account unlinked.
    /// Metadata JSON: <c>{ "provider": "google" }</c>.
    /// </summary>
    public const string OAuthUnlinked = "auth.oauth.unlinked";

    // ── Authorization / role changes ────────────────────────────────────

    /// <summary>
    /// User role changed (admin operation).
    /// Metadata JSON: <c>{ "oldRole": "user", "newRole": "admin" }</c>.
    /// ActorUserId = admin who changed the role; TargetUserId = the user.
    /// </summary>
    public const string RoleChanged = "auth.role.changed";

    /// <summary>
    /// First admin provisioned via the C5 bootstrap-admin token. This
    /// event marks a one-time, high-privilege transition and MUST be
    /// flagged for human review on every occurrence.
    /// ActorUserId = TargetUserId = the user.
    /// </summary>
    public const string BootstrapAdminCreated = "auth.bootstrap.admin";

    // ── 2FA admin overrides ─────────────────────────────────────────────

    /// <summary>
    /// Admin disabled 2FA for another user (account-recovery flow).
    /// ActorUserId = admin; TargetUserId = the user whose 2FA was removed.
    /// </summary>
    public const string Admin2FADisabled = "auth.admin.2fa.disabled";

    // ── Session lifecycle ───────────────────────────────────────────────

    /// <summary>
    /// User revoked all (or a subset of) their active sessions in one
    /// action. Metadata JSON: <c>{ "revokedCount": int, "currentSessionPreserved": bool }</c>.
    /// </summary>
    public const string BulkSessionRevoke = "auth.session.bulk_revoke";
}
