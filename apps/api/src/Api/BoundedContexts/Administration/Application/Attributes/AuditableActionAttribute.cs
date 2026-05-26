namespace Api.BoundedContexts.Administration.Application.Attributes;

/// <summary>
/// Marks a MediatR command for automatic audit logging via AuditLoggingBehavior.
/// Issue #3691: Audit Log System
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class AuditableActionAttribute : Attribute
{
    /// <summary>
    /// The action name for the audit log (e.g., "UserImpersonate", "ServiceRestart").
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// The resource type being acted upon (e.g., "User", "Service", "FeatureFlag").
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Confirmation level: 1 = Warning modal, 2 = Must type "CONFIRM".
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Selects how <c>audit_logs.user_id</c> is populated by <c>AuditLoggingBehavior</c>.
    /// Default <see cref="AuditUserIdSource.Caller"/> = the session principal's subject
    /// (the user invoking the command). For management commands where the audit row's subject
    /// is the user being ACTED UPON (e.g. <c>ImpersonationStartCommand</c>,
    /// <c>RevokeImpersonationCommand</c>), set this to <see cref="AuditUserIdSource.ResourceId"/>
    /// so the behavior writes <c>user_id = ExtractResourceId(request)</c> and
    /// <c>impersonated_user_id = session.Principal.Subject.Id</c> — matching the SP5 S1/S2
    /// convention <c>user_id = subject of the command, impersonated_user_id = actor</c>.
    /// SP5 Admin Security S2 — D-S2-3 actor mapping for management commands.
    /// </summary>
    public AuditUserIdSource UserIdSource { get; set; } = AuditUserIdSource.Caller;

    public AuditableActionAttribute(string action, string resource)
    {
        Action = action;
        Resource = resource;
    }
}

/// <summary>
/// Strategy for selecting <c>audit_logs.user_id</c> in <c>AuditLoggingBehavior</c>.
/// </summary>
internal enum AuditUserIdSource
{
    /// <summary>
    /// Default. <c>user_id</c> = the session principal's subject (the caller).
    /// Suitable for self-acting commands (e.g. UserUpdateProfile, ChangeUserRole).
    /// </summary>
    Caller = 0,

    /// <summary>
    /// <c>user_id</c> = the resource id of the command (the user being acted upon).
    /// Used for management commands such as <c>ImpersonationStartCommand</c> where the audit
    /// row's natural subject is the target (e.g. Bob being impersonated), and the actor (Alice
    /// the admin) goes into <c>impersonated_user_id</c>.
    /// </summary>
    ResourceId = 1,
}
