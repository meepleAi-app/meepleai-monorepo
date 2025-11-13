using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

// DDD CQRS imports
using DddUpdateUserProfileCommand = Api.BoundedContexts.Authentication.Application.Commands.UpdateUserProfileCommand;
using DddChangePasswordCommand = Api.BoundedContexts.Authentication.Application.Commands.ChangePasswordCommand;
using DddGetUserProfileQuery = Api.BoundedContexts.Authentication.Application.Queries.GetUserProfileQuery;

namespace Api.Routing;

/// <summary>
/// User profile management endpoints.
/// Handles profile viewing, updating display name/email, and password changes.
/// </summary>
public static class UserProfileEndpoints
{
    public static RouteGroupBuilder MapUserProfileEndpoints(this RouteGroupBuilder group)
    {
        // Get user profile (AUTH-PROFILE-01)
        group.MapGet("/users/profile", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var query = new DddGetUserProfileQuery { UserId = Guid.Parse(session.User.Id) };
                var profile = await mediator.Send(query, ct);

                if (profile == null)
                {
                    logger.LogWarning("Profile not found for user {UserId}", session.User.Id);
                    return Results.NotFound(new { error = "Profile not found" });
                }

                return Results.Json(profile);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            catch (Exception ex)
            {
                logger.LogError(ex, "Get profile error for user {UserId}", session.User.Id);
                return Results.Problem(detail: "An error occurred retrieving your profile", statusCode: 500);
            }
#pragma warning restore CA1031
        })
        .RequireAuthorization()
        .WithName("GetUserProfile")
        .WithTags("User Profile")
        .WithSummary("Get current user's profile")
        .WithDescription(@"Returns complete profile information for the authenticated user including email, display name, role, and 2FA status.

**Authorization**: Requires active session (cookie-based authentication).

**Response**: UserProfileDto object with profile data.")
        .Produces(200)
        .Produces(401)
        .Produces(404);

        // Update user profile (AUTH-PROFILE-02)
        group.MapPut("/users/profile", async (
            [FromBody] UpdateProfilePayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var command = new DddUpdateUserProfileCommand
                {
                    UserId = Guid.Parse(session.User.Id),
                    DisplayName = payload.DisplayName,
                    Email = payload.Email
                };

                await mediator.Send(command, ct);
                logger.LogInformation("Profile updated for user {UserId}", session.User.Id);

                return Results.Json(new { ok = true, message = "Profile updated successfully" });
            }
            catch (Api.SharedKernel.Domain.Exceptions.ValidationException ex)
            {
                logger.LogWarning(ex, "Profile update validation failed for user {UserId}", session.User.Id);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Api.SharedKernel.Domain.Exceptions.DomainException ex)
            {
                logger.LogWarning(ex, "Profile update domain validation failed for user {UserId}", session.User.Id);
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            catch (Exception ex)
            {
                logger.LogError(ex, "Update profile error for user {UserId}", session.User.Id);
                return Results.Problem(detail: "An error occurred updating your profile", statusCode: 500);
            }
#pragma warning restore CA1031
        })
        .RequireAuthorization()
        .WithName("UpdateUserProfile")
        .WithTags("User Profile")
        .WithSummary("Update current user's profile")
        .WithDescription(@"Updates the authenticated user's profile information (display name and/or email).

**Authorization**: Requires active session (cookie-based authentication).

**Request Body**: UpdateProfilePayload with optional displayName and email fields.

**Validation**:
- Display name cannot be empty
- Email must be valid format
- Email must not be in use by another user

**Security**: Users can only update their own profile.")
        .Produces(200)
        .Produces(400)
        .Produces(401);

        // Change password (AUTH-PROFILE-03)
        group.MapPut("/users/profile/password", async (
            [FromBody] ChangePasswordPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var command = new DddChangePasswordCommand
                {
                    UserId = Guid.Parse(session.User.Id),
                    CurrentPassword = payload.CurrentPassword,
                    NewPassword = payload.NewPassword
                };

                await mediator.Send(command, ct);
                logger.LogInformation("Password changed for user {UserId}", session.User.Id);

                return Results.Json(new { ok = true, message = "Password changed successfully" });
            }
            catch (Api.SharedKernel.Domain.Exceptions.ValidationException ex)
            {
                logger.LogWarning(ex, "Password change validation failed for user {UserId}", session.User.Id);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Api.SharedKernel.Domain.Exceptions.DomainException ex)
            {
                logger.LogWarning(ex, "Password change domain validation failed for user {UserId}", session.User.Id);
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            catch (Exception ex)
            {
                logger.LogError(ex, "Change password error for user {UserId}", session.User.Id);
                return Results.Problem(detail: "An error occurred changing your password", statusCode: 500);
            }
#pragma warning restore CA1031
        })
        .RequireAuthorization()
        .WithName("ChangePassword")
        .WithTags("User Profile")
        .WithSummary("Change current user's password")
        .WithDescription(@"Changes the authenticated user's password with current password verification.

**Authorization**: Requires active session (cookie-based authentication).

**Request Body**: ChangePasswordPayload with currentPassword and newPassword.

**Validation**:
- Current password must be correct
- New password cannot be empty

**Security**:
- Requires current password verification
- Users can only change their own password
- Password hash uses PBKDF2 with 210k iterations")
        .Produces(200)
        .Produces(400)
        .Produces(401);

        return group;
    }
}

/// <summary>
/// Payload for updating user profile.
/// </summary>
public record UpdateProfilePayload(string? DisplayName, string? Email);

/// <summary>
/// Payload for changing password.
/// </summary>
public record ChangePasswordPayload(string CurrentPassword, string NewPassword);
