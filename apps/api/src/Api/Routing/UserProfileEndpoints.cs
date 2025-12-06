using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

// DDD CQRS imports
using DddUpdateUserProfileCommand = Api.BoundedContexts.Authentication.Application.Commands.UpdateUserProfileCommand;
using DddChangePasswordCommand = Api.BoundedContexts.Authentication.Application.Commands.ChangePasswordCommand;
using DddUpdatePreferencesCommand = Api.BoundedContexts.Authentication.Application.Commands.UpdatePreferencesCommand;
using DddGetUserProfileQuery = Api.BoundedContexts.Authentication.Application.Queries.GetUserProfileQuery;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;

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
            // Session validated by RequireSessionFilter
            var session = (ActiveSession)context.Items[nameof(ActiveSession)]!;

            var query = new DddGetUserProfileQuery { UserId = Guid.Parse(session.User.Id) };
            var profile = await mediator.Send(query, ct).ConfigureAwait(false);

            if (profile == null)
            {
                logger.LogWarning("Profile not found for user {UserId}", session.User.Id);
                return Results.NotFound(new { error = "Profile not found" });
            }

            return Results.Json(profile);
        })
        .RequireSession() // Issue #1446: Automatic session validation
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
            // Session validated by RequireSessionFilter
            var session = (ActiveSession)context.Items[nameof(ActiveSession)]!;

            var command = new DddUpdateUserProfileCommand
            {
                UserId = Guid.Parse(session.User.Id),
                DisplayName = payload.DisplayName,
                Email = payload.Email
            };

            await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Profile updated for user {UserId}", session.User.Id);

            return Results.Json(new { ok = true, message = "Profile updated successfully" });
        })
        .RequireSession() // Issue #1446: Automatic session validation
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
            // Session validated by RequireSessionFilter
            var session = (ActiveSession)context.Items[nameof(ActiveSession)]!;

            var command = new DddChangePasswordCommand
            {
                UserId = Guid.Parse(session.User.Id),
                CurrentPassword = payload.CurrentPassword,
                NewPassword = payload.NewPassword
            };

            await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Password changed for user {UserId}", session.User.Id);

            return Results.Json(new { ok = true, message = "Password changed successfully" });
        })
        .RequireSession() // Issue #1446: Automatic session validation
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

        // Get user's PDF upload quota (USER-QUOTA-01)
        group.MapGet("/users/me/upload-quota", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (ActiveSession)context.Items[nameof(ActiveSession)]!;

            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            var query = new GetUserUploadQuotaQuery(userId);
            var quotaInfo = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Json(quotaInfo);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("GetUserUploadQuota")
        .WithTags("User Profile")
        .WithSummary("Get current user's PDF upload quota")
        .WithDescription(@"Returns PDF upload quota information for the authenticated user including:
- Daily and weekly upload limits based on user tier (free/normal/premium)
- Current usage counts for daily and weekly periods
- Remaining uploads before hitting limits
- Reset times for daily and weekly quotas

**User Tiers**:
- **Free**: 5 PDF/day, 20 PDF/week
- **Normal**: 20 PDF/day, 100 PDF/week
- **Premium**: 100 PDF/day, 500 PDF/week
- **Admin/Editor**: Unlimited (bypass quota checks)

**Authorization**: Requires active session (cookie-based authentication).

**Response**: PdfUploadQuotaInfo object with quota details.")
        .Produces(200)
        .Produces(400)
        .Produces(401);

        // Update user preferences (AUTH-PROFILE-04)
        group.MapPut("/users/preferences", async (
            [FromBody] UpdatePreferencesPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (ActiveSession)context.Items[nameof(ActiveSession)]!;

            var command = new DddUpdatePreferencesCommand
            {
                UserId = Guid.Parse(session.User.Id),
                Language = payload.Language,
                Theme = payload.Theme,
                EmailNotifications = payload.EmailNotifications,
                DataRetentionDays = payload.DataRetentionDays
            };

            var updatedProfile = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Preferences updated for user {UserId}", session.User.Id);

            return Results.Json(updatedProfile);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("UpdateUserPreferences")
        .WithTags("User Profile")
        .WithSummary("Update user preferences")
        .WithDescription(@"Updates user preferences including language, theme, email notifications, and data retention settings.

**Authorization**: Requires active session (cookie-based authentication).

**Request Body**: UpdatePreferencesPayload with language, theme, emailNotifications, dataRetentionDays.

**Response**: Updated UserProfileDto with all profile information including new preferences.")
        .Produces<UserProfileDto>(200)
        .Produces(400)
        .Produces(401);

        // Get user preferences (AUTH-PROFILE-05)
        group.MapGet("/users/preferences", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (ActiveSession)context.Items[nameof(ActiveSession)]!;

            var query = new DddGetUserProfileQuery
            {
                UserId = Guid.Parse(session.User.Id)
            };

            var userProfile = await mediator.Send(query, ct).ConfigureAwait(false);
            logger.LogInformation("Preferences retrieved for user {UserId}", session.User.Id);

            // Extract preferences from full profile
            var preferences = new
            {
                language = userProfile!.Language,
                theme = userProfile.Theme,
                emailNotifications = userProfile.EmailNotifications,
                dataRetentionDays = userProfile.DataRetentionDays
            };

            return Results.Json(preferences);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetUserPreferences")
        .WithTags("User Profile")
        .WithSummary("Get user preferences")
        .WithDescription(@"Retrieves user preferences including language, theme, email notifications, and data retention settings.

**Authorization**: Requires active session (cookie-based authentication).

**Response**: UserPreferences object with current settings.")
        .Produces(200)
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

/// <summary>
/// Payload for updating user preferences.
/// </summary>
public record UpdatePreferencesPayload(
    string Language,
    string Theme,
    bool EmailNotifications,
    int DataRetentionDays);
