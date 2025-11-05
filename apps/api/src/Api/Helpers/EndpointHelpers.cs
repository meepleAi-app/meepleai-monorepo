using Api.Infrastructure.Entities;
using Microsoft.AspNetCore.Http;

namespace Api.Helpers;

/// <summary>
/// Common helper methods for endpoints to reduce code duplication.
/// </summary>
public static class EndpointHelpers
{
    /// <summary>
    /// Gets the active session from HttpContext, returning null if not found or invalid.
    /// </summary>
    public static ActiveSession? GetActiveSession(HttpContext context)
    {
        if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
        {
            return null;
        }
        return session;
    }

    /// <summary>
    /// Checks if the current user has the specified role.
    /// </summary>
    public static bool HasRole(ActiveSession session, UserRole role)
    {
        return string.Equals(session.User.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the current user is an admin.
    /// </summary>
    public static bool IsAdmin(ActiveSession session)
    {
        return HasRole(session, UserRole.Admin);
    }

    /// <summary>
    /// Checks if the current user is an editor or admin.
    /// </summary>
    public static bool IsEditorOrAdmin(ActiveSession session)
    {
        return HasRole(session, UserRole.Editor) || HasRole(session, UserRole.Admin);
    }

    /// <summary>
    /// Returns an unauthorized result.
    /// </summary>
    public static IResult Unauthorized()
    {
        return Results.Unauthorized();
    }

    /// <summary>
    /// Returns a forbidden result.
    /// </summary>
    public static IResult Forbidden()
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Returns a bad request result with an error message.
    /// </summary>
    public static IResult BadRequest(string error)
    {
        return Results.BadRequest(new { error });
    }

    /// <summary>
    /// Checks authentication and returns the session if valid, or an unauthorized result.
    /// </summary>
    public static (ActiveSession? Session, IResult? ErrorResult) CheckAuth(HttpContext context)
    {
        var session = GetActiveSession(context);
        if (session == null)
        {
            return (null, Unauthorized());
        }
        return (session, null);
    }

    /// <summary>
    /// Checks authentication and admin role, returning the session if valid or an error result.
    /// </summary>
    public static (ActiveSession? Session, IResult? ErrorResult) CheckAdminAuth(HttpContext context)
    {
        var (session, errorResult) = CheckAuth(context);
        if (session == null)
        {
            return (null, errorResult);
        }

        if (!IsAdmin(session))
        {
            return (null, Forbidden());
        }

        return (session, null);
    }

    /// <summary>
    /// Checks authentication and editor/admin role, returning the session if valid or an error result.
    /// </summary>
    public static (ActiveSession? Session, IResult? ErrorResult) CheckEditorOrAdminAuth(HttpContext context)
    {
        var (session, errorResult) = CheckAuth(context);
        if (session == null)
        {
            return (null, errorResult);
        }

        if (!IsEditorOrAdmin(session))
        {
            return (null, Forbidden());
        }

        return (session, null);
    }

    /// <summary>
    /// Validates that a string parameter is not null or whitespace.
    /// Returns null if valid, or a BadRequest result if invalid.
    /// </summary>
    public static IResult? ValidateRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return BadRequest($"{parameterName} is required");
        }
        return null;
    }

    /// <summary>
    /// Validates multiple required string parameters.
    /// Returns null if all valid, or a BadRequest result for the first invalid parameter.
    /// </summary>
    public static IResult? ValidateRequired(params (string? Value, string Name)[] parameters)
    {
        foreach (var (value, name) in parameters)
        {
            var error = ValidateRequired(value, name);
            if (error != null)
            {
                return error;
            }
        }
        return null;
    }
}
