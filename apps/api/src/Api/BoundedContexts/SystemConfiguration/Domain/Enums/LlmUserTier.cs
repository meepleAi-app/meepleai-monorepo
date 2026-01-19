namespace Api.BoundedContexts.SystemConfiguration.Domain.Enums;

/// <summary>
/// User tier for LLM model routing configuration.
/// Issue #2596: Migrate LLM tier routing to database with test/production model separation.
/// </summary>
/// <remarks>
/// This enum maps to user roles for LLM model selection:
/// - Anonymous: Unauthenticated users (free tier models)
/// - User: Authenticated basic users
/// - Editor: Content editors with elevated permissions
/// - Admin: Administrators with full access
/// - Premium: Premium tier users with priority models
/// </remarks>
public enum LlmUserTier
{
    /// <summary>
    /// Unauthenticated users - typically routed to free tier models.
    /// </summary>
    Anonymous = 0,

    /// <summary>
    /// Authenticated basic users - balanced cost/quality models.
    /// </summary>
    User = 1,

    /// <summary>
    /// Content editors - higher quality models for content creation.
    /// </summary>
    Editor = 2,

    /// <summary>
    /// Administrators - priority access to best models.
    /// </summary>
    Admin = 3,

    /// <summary>
    /// Premium tier users - highest quality models.
    /// </summary>
    Premium = 4
}
