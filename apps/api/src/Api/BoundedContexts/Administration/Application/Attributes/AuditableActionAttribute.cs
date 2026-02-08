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

    public AuditableActionAttribute(string action, string resource)
    {
        Action = action;
        Resource = resource;
    }
}
