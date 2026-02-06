namespace Api.Infrastructure.Authorization;

/// <summary>
/// Metadata attribute to mark endpoints that require user confirmation.
/// Used for frontend to display appropriate confirmation modals and for audit logging.
/// </summary>
/// <example>
/// <code>
/// app.MapDelete("/api/v1/admin/cache/clear-all", async () => ...)
///    .WithMetadata(new RequiresConfirmationAttribute(ConfirmationLevel.Level2))
///    .RequireAuthorization("RequireSuperAdmin");
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresConfirmationAttribute : Attribute
{
    /// <summary>
    /// The confirmation level required for this endpoint
    /// </summary>
    public ConfirmationLevel Level { get; }

    /// <summary>
    /// Human-readable action description for confirmation modal
    /// </summary>
    public string ActionDescription { get; }

    /// <summary>
    /// Optional warning message to display to the user
    /// </summary>
    public string? WarningMessage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresConfirmationAttribute"/> class.
    /// </summary>
    /// <param name="level">The confirmation level required</param>
    /// <param name="actionDescription">Human-readable action description</param>
    /// <param name="warningMessage">Optional warning message</param>
    public RequiresConfirmationAttribute(
        ConfirmationLevel level,
        string actionDescription,
        string? warningMessage = null)
    {
        Level = level;
        ActionDescription = actionDescription;
        WarningMessage = warningMessage;
    }
}
