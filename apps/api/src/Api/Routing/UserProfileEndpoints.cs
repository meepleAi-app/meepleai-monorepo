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
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// User profile management endpoints.
/// Handles profile viewing, updating display name/email, and password changes.
/// </summary>
internal static class UserProfileEndpoints
{
    public static RouteGroupBuilder MapUserProfileEndpoints(this RouteGroupBuilder group)
    {
        // AUTH-PROFILE-01: User profile management
        MapProfileManagementEndpoints(group);
        // USER-QUOTA-01: User quota management
        MapQuotaEndpoints(group);
        // AUTH-PROFILE-04: User preferences
        MapPreferenceEndpoints(group);
        // USER-ACTIVITY-01: User activity timeline
        MapActivityEndpoints(group);
        // Issue #3074: User AI usage tracking
        MapAiUsageEndpoints(group);
        // Issue #3674: User available features
        MapFeatureAccessEndpoints(group);

        return group;
    }

    private static void MapProfileManagementEndpoints(RouteGroupBuilder group)
    {
        MapGetUserProfile(group);
        MapUpdateUserProfile(group);
        MapChangePassword(group);
    }

    private static void MapGetUserProfile(RouteGroupBuilder group)
    {
        // Get user profile (AUTH-PROFILE-01)
        group.MapGet("/users/profile", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new DddGetUserProfileQuery { UserId = session!.User!.Id };
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
    }

    private static void MapUpdateUserProfile(RouteGroupBuilder group)
    {
        // Update user profile (AUTH-PROFILE-02)
        group.MapPut("/users/profile", async (
            [FromBody] UpdateProfilePayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new DddUpdateUserProfileCommand
            {
                UserId = session!.User!.Id,
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
    }

    private static void MapChangePassword(RouteGroupBuilder group)
    {
        // Change password (AUTH-PROFILE-03)
        group.MapPut("/users/profile/password", async (
            [FromBody] ChangePasswordPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new DddChangePasswordCommand
            {
                UserId = session!.User!.Id,
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
    }

    private static void MapQuotaEndpoints(RouteGroupBuilder group)
    {
        // Get user's PDF upload quota (USER-QUOTA-01)
        group.MapGet("/users/me/upload-quota", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // session.User.Id is already a Guid from SessionStatusDto
            var userId = session!.User!.Id;

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
    }

    private static void MapPreferenceEndpoints(RouteGroupBuilder group)
    {
        MapUpdateUserPreferences(group);
        MapGetUserPreferences(group);
    }

    private static void MapUpdateUserPreferences(RouteGroupBuilder group)
    {
        // Update user preferences (AUTH-PROFILE-04)
        group.MapPut("/users/preferences", async (
            [FromBody] UpdatePreferencesPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new DddUpdatePreferencesCommand
            {
                UserId = session!.User!.Id,
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
    }

    private static void MapGetUserPreferences(RouteGroupBuilder group)
    {
        // Get user preferences (AUTH-PROFILE-05)
        group.MapGet("/users/preferences", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new DddGetUserProfileQuery
            {
                UserId = session!.User!.Id
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
    }

    private static void MapActivityEndpoints(RouteGroupBuilder group)
    {
        // Get user activity timeline (USER-ACTIVITY-01 - Issue #911)
        group.MapGet("/users/me/activity", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            string? actionFilter = null,
            string? resourceFilter = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int limit = 100,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new Api.BoundedContexts.Administration.Application.Queries.GetUserActivityQuery(
                UserId: session!.User!.Id,
                ActionFilter: actionFilter,
                ResourceFilter: resourceFilter,
                StartDate: startDate,
                EndDate: endDate,
                Limit: limit
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            logger.LogInformation("Activity timeline retrieved for user {UserId}: {Count} activities", session.User.Id, result.Activities.Count);

            return Results.Json(result);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetMyActivity")
        .WithTags("User Profile")
        .WithSummary("Get current user's activity timeline")
        .WithDescription(@"Retrieves audit log timeline for the authenticated user with optional filtering.

**Issue**: #911 - UserActivityTimeline component backend support.

**Authorization**: Requires active session (cookie-based authentication). Users can only see their own activity.

**Query Parameters**:
- `actionFilter` (optional): Comma-separated list of action types to filter (e.g., 'Login,PasswordChanged')
- `resourceFilter` (optional): Filter by resource type (e.g., 'User', 'Game')
- `startDate` (optional): ISO 8601 timestamp - filter logs from this date
- `endDate` (optional): ISO 8601 timestamp - filter logs until this date
- `limit` (optional): Maximum number of logs to return (default: 100, max: 500)

**Response**: GetUserActivityResult with filtered activities and total count.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapAiUsageEndpoints(RouteGroupBuilder group)
    {
        // Get user's AI usage summary (Issue #3074, enhanced in Issue #3338)
        group.MapGet("/users/me/ai-usage", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            int days = 30,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var startDate = endDate.AddDays(-days);

            // Issue #3338: Use detailed query for enhanced response
            var query = new GetUserDetailedAiUsageQuery(userId, startDate, endDate);
            var usage = await mediator.Send(query, ct).ConfigureAwait(false);

            logger.LogInformation("AI usage retrieved for user {UserId}: {TotalTokens} tokens, ${TotalCost:F6} over {Days} days",
                userId, usage.TotalTokens, usage.TotalCostUsd, days);

            return Results.Ok(usage);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetMyAiUsage")
        .WithTags("User Profile", "AI Usage")
        .WithSummary("Get current user's AI usage summary")
        .WithDescription(@"Returns detailed AI usage statistics for the authenticated user including:
- Total tokens consumed and cost
- Token/cost breakdown by model
- Usage breakdown by operation type (chat, rag_query, embedding)
- Daily usage time series

**Issue**: #3074 - AI Token Usage Tracking Backend, enhanced in Issue #3338.

**Authorization**: Requires active session (cookie-based authentication). Users can only see their own AI usage.

**Query Parameters**:
- `days` (optional): Number of days to look back (default: 30, max: 365)

**Response**: UserAiUsageDto with detailed usage breakdown.")
        .Produces<UserAiUsageDto>(200)
        .Produces(401);

        // Issue #94: Multi-period usage summary (today/7d/30d)
        group.MapGet("/users/me/ai-usage/summary", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var query = new GetMyAiUsageSummaryQuery(session!.User!.Id);
            return Results.Ok(await mediator.Send(query, ct).ConfigureAwait(false));
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetMyAiUsageSummary")
        .WithTags("AI Usage")
        .WithSummary("Get multi-period AI usage summary (today/7d/30d)")
        .Produces<AiUsageSummaryDto>(200)
        .Produces(401);

        // Issue #94: Usage distributions (model, provider, operation)
        group.MapGet("/users/me/ai-usage/distributions", async (
            HttpContext context,
            IMediator mediator,
            int days = 30,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var clampedDays = Math.Clamp(days, 1, 90);
            var query = new GetMyAiUsageDistributionsQuery(session!.User!.Id, clampedDays);
            return Results.Ok(await mediator.Send(query, ct).ConfigureAwait(false));
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetMyAiUsageDistributions")
        .WithTags("AI Usage")
        .WithSummary("Get AI usage distribution breakdowns (model, provider, operation)")
        .Produces<AiUsageDistributionsDto>(200)
        .Produces(401);

        // Issue #94: Recent individual requests (last 7 days, paginated)
        group.MapGet("/users/me/ai-usage/recent", async (
            HttpContext context,
            IMediator mediator,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var query = new GetMyAiUsageRecentQuery(session!.User!.Id, page, pageSize);
            return Results.Ok(await mediator.Send(query, ct).ConfigureAwait(false));
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetMyAiUsageRecent")
        .WithTags("AI Usage")
        .WithSummary("Get recent AI requests (last 7 days, paginated)")
        .Produces<AiUsageRecentDto>(200)
        .Produces(401);
    }

    private static void MapFeatureAccessEndpoints(RouteGroupBuilder group)
    {
        // Get user's available features based on role and tier (Issue #3674)
        group.MapGet("/users/me/features", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            logger.LogInformation("Retrieving available features for user {UserId}", userId);

            var query = new GetUserAvailableFeaturesQuery { UserId = userId };
            var features = await mediator.Send(query, ct).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} has access to {AccessCount}/{TotalCount} features",
                userId, features.Count(f => f.HasAccess), features.Count);

            return Results.Ok(features);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetMyFeatures")
        .WithTags("User Profile", "Features")
        .WithSummary("Get features available to current user")
        .WithDescription(@"Returns all feature flags with access status based on user's role and tier.

**Issue**: #3674 - Feature Flags Verification

**Authorization**: Requires active session. Users see all features with their access status.

**Response**: List of UserFeatureDto showing which features the user can access.")
        .Produces<IReadOnlyList<UserFeatureDto>>(200)
        .Produces(401);
    }
}

/// <summary>
/// Payload for updating user profile.
/// </summary>
internal record UpdateProfilePayload(string? DisplayName, string? Email);

/// <summary>
/// Payload for changing password.
/// </summary>
internal record ChangePasswordPayload(string CurrentPassword, string NewPassword);

/// <summary>
/// Payload for updating user preferences.
/// </summary>
internal record UpdatePreferencesPayload(
    string Language,
    string Theme,
    bool EmailNotifications,
    int DataRetentionDays);
