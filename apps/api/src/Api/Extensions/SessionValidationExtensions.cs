using Api.Infrastructure.Entities;
using Api.Models;

namespace Api.Extensions;

/// <summary>
/// Extension methods for session and authentication validation in API endpoints.
/// Eliminates 500+ lines of duplicated validation code across 139 endpoint occurrences.
///
/// Pattern: Extension methods on HttpContext that return tuples with:
/// - Authentication/Authorization status (bool)
/// - Session object if authenticated (ActiveSession?)
/// - Error result if validation failed (IResult?)
///
/// Usage:
/// <code>
/// var (authorized, session, error) = context.RequireAdminSession();
/// if (!authorized) return error!;
/// // Use session...
/// </code>
/// </summary>
public static class SessionValidationExtensions
{
    /// <summary>
    /// Validates that an active session exists in the HttpContext.Items collection.
    /// </summary>
    /// <param name="context">The HTTP context containing session data.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthenticated: true if session exists and is valid
    /// - Session: The ActiveSession object if authenticated, null otherwise
    /// - ErrorResult: IResult to return if not authenticated (Unauthorized), null if successful
    /// </returns>
    /// <example>
    /// var (authenticated, session, error) = context.TryGetActiveSession();
    /// if (!authenticated) return error!;
    /// // Use session...
    /// </example>
    public static (bool IsAuthenticated, ActiveSession Session, IResult? ErrorResult)
        TryGetActiveSession(this HttpContext context)
    {
        if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) ||
            value is not ActiveSession session)
        {
            return (false, default!, Results.Unauthorized());
        }

        return (true, session, null);
    }

    /// <summary>
    /// Validates authentication using either session cookies OR API key authentication.
    /// Supports dual authentication modes for maximum flexibility.
    /// </summary>
    /// <param name="context">The HTTP context containing authentication data.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthenticated: true if either session or API key is valid
    /// - Session: The ActiveSession if session auth used, null if API key auth
    /// - ErrorResult: IResult to return if neither auth method valid, null if successful
    /// </returns>
    /// <example>
    /// var (authenticated, session, error) = context.TryGetAuthenticatedUser();
    /// if (!authenticated) return error!;
    /// // session may be null if API key auth was used
    /// </example>
    public static (bool IsAuthenticated, ActiveSession? Session, IResult? ErrorResult)
        TryGetAuthenticatedUser(this HttpContext context)
    {
        var hasSession = context.Items.TryGetValue(nameof(ActiveSession), out var value) &&
                        value is ActiveSession session;
        var hasApiKey = context.User.Identity?.IsAuthenticated == true;

        if (!hasSession && !hasApiKey)
        {
            return (false, null, Results.Unauthorized());
        }

        return (true, hasSession ? (ActiveSession)value! : null, null);
    }

    /// <summary>
    /// Validates that the authenticated user has a specific role.
    /// </summary>
    /// <param name="session">The active session containing user information.</param>
    /// <param name="requiredRole">The role required for authorization.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthorized: true if user has the required role
    /// - ErrorResult: IResult to return if not authorized (Forbidden), null if authorized
    /// </returns>
    /// <example>
    /// var (authenticated, session, error) = context.TryGetActiveSession();
    /// if (!authenticated) return error!;
    /// var (authorized, forbidResult) = session.RequireRole(UserRole.Admin);
    /// if (!authorized) return forbidResult!;
    /// </example>
    public static (bool IsAuthorized, IResult? ErrorResult)
        RequireRole(this ActiveSession session, UserRole requiredRole)
    {
        if (!string.Equals(session.User.Role, requiredRole.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return (false, Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        return (true, null);
    }

    /// <summary>
    /// Validates that the authenticated user has Admin role.
    /// Convenience method for the most common role check.
    /// </summary>
    /// <param name="session">The active session containing user information.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthorized: true if user is Admin
    /// - ErrorResult: IResult to return if not authorized (Forbidden), null if authorized
    /// </returns>
    /// <example>
    /// var (authenticated, session, error) = context.TryGetActiveSession();
    /// if (!authenticated) return error!;
    /// var (authorized, forbidResult) = session.RequireAdminRole();
    /// if (!authorized) return forbidResult!;
    /// </example>
    public static (bool IsAuthorized, IResult? ErrorResult)
        RequireAdminRole(this ActiveSession session)
    {
        return session.RequireRole(UserRole.Admin);
    }

    /// <summary>
    /// Validates that the authenticated user has either Admin or Editor role.
    /// Common pattern for content management endpoints.
    /// </summary>
    /// <param name="session">The active session containing user information.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthorized: true if user is Admin or Editor
    /// - ErrorResult: IResult to return if not authorized (Forbid), null if authorized
    /// </returns>
    /// <example>
    /// var (authenticated, session, error) = context.TryGetActiveSession();
    /// if (!authenticated) return error!;
    /// var (authorized, forbidResult) = session.RequireAdminOrEditorRole();
    /// if (!authorized) return forbidResult!;
    /// </example>
    public static (bool IsAuthorized, IResult? ErrorResult)
        RequireAdminOrEditorRole(this ActiveSession session)
    {
        var isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(),
            StringComparison.OrdinalIgnoreCase);
        var isEditor = string.Equals(session.User.Role, UserRole.Editor.ToString(),
            StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isEditor)
        {
            return (false, Results.Forbid());
        }

        return (true, null);
    }

    /// <summary>
    /// Combined validation: Gets active session and validates required role in one call.
    /// Most efficient pattern for role-restricted endpoints.
    /// </summary>
    /// <param name="context">The HTTP context containing session data.</param>
    /// <param name="requiredRole">The role required for authorization.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthorized: true if session exists and user has required role
    /// - Session: The ActiveSession if authorized, null otherwise
    /// - ErrorResult: IResult to return if not authorized (Unauthorized or Forbidden), null if successful
    /// </returns>
    /// <example>
    /// var (authorized, session, error) = context.RequireSessionWithRole(UserRole.Admin);
    /// if (!authorized) return error!;
    /// // Use session...
    /// </example>
    public static (bool IsAuthorized, ActiveSession Session, IResult? ErrorResult)
        RequireSessionWithRole(this HttpContext context, UserRole requiredRole)
    {
        var (authenticated, session, authError) = context.TryGetActiveSession();
        if (!authenticated) return (false, default!, authError);

        var (authorized, roleError) = session.RequireRole(requiredRole);
        if (!authorized) return (false, default!, roleError);

        return (true, session, null);
    }

    /// <summary>
    /// Combined validation: Gets active session and validates Admin role in one call.
    /// Most efficient pattern for admin-only endpoints.
    /// </summary>
    /// <param name="context">The HTTP context containing session data.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthorized: true if session exists and user is Admin
    /// - Session: The ActiveSession if authorized, null otherwise
    /// - ErrorResult: IResult to return if not authorized, null if successful
    /// </returns>
    /// <example>
    /// var (authorized, session, error) = context.RequireAdminSession();
    /// if (!authorized) return error!;
    /// // Use session...
    /// </example>
    public static (bool IsAuthorized, ActiveSession Session, IResult? ErrorResult)
        RequireAdminSession(this HttpContext context)
    {
        return context.RequireSessionWithRole(UserRole.Admin);
    }

    /// <summary>
    /// Combined validation: Gets active session and validates Admin or Editor role in one call.
    /// Most efficient pattern for content management endpoints.
    /// </summary>
    /// <param name="context">The HTTP context containing session data.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsAuthorized: true if session exists and user is Admin or Editor
    /// - Session: The ActiveSession if authorized, null otherwise
    /// - ErrorResult: IResult to return if not authorized, null if successful
    /// </returns>
    /// <example>
    /// var (authorized, session, error) = context.RequireAdminOrEditorSession();
    /// if (!authorized) return error!;
    /// // Use session...
    /// </example>
    public static (bool IsAuthorized, ActiveSession Session, IResult? ErrorResult)
        RequireAdminOrEditorSession(this HttpContext context)
    {
        var (authenticated, session, authError) = context.TryGetActiveSession();
        if (!authenticated) return (false, default!, authError);

        var (authorized, roleError) = session.RequireAdminOrEditorRole();
        if (!authorized) return (false, default!, roleError);

        return (true, session, null);
    }
}
