using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.Commands.AccountLockout;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for user activity, library stats, quick actions, bulk operations, and impersonation.
/// </summary>
internal static class AdminUserActivityEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapBulkUserEndpoints(group);
        MapUserActivityEndpoints(group);
        MapUserLibraryStatsEndpoints(group);
        MapUserQuickActionsEndpoints(group);
        MapUserImpersonateEndpoint(group);
        MapEndImpersonationEndpoint(group);
    }

    private static void MapBulkUserEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/admin/users/bulk/password-reset", HandleBulkPasswordReset)
        .WithName("BulkPasswordReset")
        .WithTags("Admin")
        .WithSummary("Reset passwords for multiple users")
        .Produces<BulkOperationResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/admin/users/bulk/role-change", HandleBulkRoleChange)
        .WithName("BulkRoleChange")
        .WithTags("Admin")
        .WithSummary("Change role for multiple users")
        .Produces<BulkOperationResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/admin/users/bulk/import", HandleBulkImportUsers)
        .WithName("BulkImportUsers")
        .WithTags("Admin")
        .WithSummary("Import users from CSV file")
        .WithDescription("CSV format: email,displayName,role,password")
        .Accepts<string>("text/csv")
        .Produces<BulkOperationResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/admin/users/bulk/export", HandleBulkExportUsers)
        .WithName("BulkExportUsers")
        .WithTags("Admin")
        .WithSummary("Export users to CSV file")
        .WithDescription("Returns CSV with format: email,displayName,role,createdAt")
        .Produces<string>(StatusCodes.Status200OK, "text/csv")
        .Produces(StatusCodes.Status401Unauthorized);

        // Suspend/Unsuspend endpoints - Issue #2886
        group.MapPost("/admin/users/{id}/suspend", HandleSuspendUser)
        .WithName("SuspendUser")
        .WithTags("Admin")
        .WithSummary("Suspend a user account")
        .WithDescription("Suspended users cannot login until unsuspended.")
        .Produces<Api.Models.UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/admin/users/{id}/unsuspend", HandleUnsuspendUser)
        .WithName("UnsuspendUser")
        .WithTags("Admin")
        .WithSummary("Unsuspend (reactivate) a user account")
        .Produces<Api.Models.UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // ISSUE-3339: Account Lockout - Unlock endpoint
        group.MapPost("/admin/users/{id}/unlock", HandleUnlockAccount)
        .WithName("UnlockAccount")
        .WithTags("Admin")
        .WithSummary("Unlock a locked user account")
        .WithDescription(@"Manually unlock a user account that was locked due to too many failed login attempts.

**Authorization**: Admin only

**Behavior**:
- Resets failed login attempts counter to 0
- Clears lockout timestamp
- Creates audit log entry

**Issue**: #3339 - Account Lockout After Failed Login Attempts")
        .Produces<UnlockAccountResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // ISSUE-3339: Get account lockout status
        group.MapGet("/admin/users/{id}/lockout-status", HandleGetLockoutStatus)
        .WithName("GetLockoutStatus")
        .WithTags("Admin")
        .WithSummary("Get account lockout status")
        .WithDescription(@"Get the current lockout status for a user account.

**Authorization**: Admin only

**Response**: Lockout status including failed attempts count, lock status, and remaining lockout time

**Issue**: #3339 - Account Lockout After Failed Login Attempts")
        .Produces<AccountLockoutStatusDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapUserActivityEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/admin/users/{userId}/activity", HandleGetUserActivity)
        .WithName("GetUserActivity")
        .WithTags("Admin")
        .WithSummary("Get user's activity timeline (admin)")
        .WithDescription(@"Retrieves audit log timeline for any user with optional filtering.

**Issue**: #911 - UserActivityTimeline component backend support.

**Authorization**: Admin only. Allows admins to view activity of any user.

**Query Parameters**:
- `actionFilter` (optional): Comma-separated list of action types to filter (e.g., 'Login,PasswordChanged')
- `resourceFilter` (optional): Filter by resource type (e.g., 'User', 'Game')
- `startDate` (optional): ISO 8601 timestamp - filter logs from this date
- `endDate` (optional): ISO 8601 timestamp - filter logs until this date
- `limit` (optional): Maximum number of logs to return (default: 100, max: 500)

**Response**: GetUserActivityResult with filtered activities and total count.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403);
    }

    private static void MapUserLibraryStatsEndpoints(RouteGroupBuilder group)
    {
        // Get user library statistics (admin only) - Issue #3139
        group.MapGet("/admin/users/{userId:guid}/library/stats", HandleGetUserLibraryStats)
            .RequireAdminSession()
            .WithName("GetUserLibraryStats")
            .WithTags("Admin", "UserLibrary")
            .WithDescription("Retrieve library statistics for a specific user (admin only)")
            .Produces<AdminUserLibraryStatsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapUserQuickActionsEndpoints(RouteGroupBuilder group)
    {
        // Reset user password (admin only) - Issue #2890
        group.MapPost("/admin/users/{userId:guid}/reset-password", HandleResetUserPassword)
            .RequireAdminSession()
            .WithName("ResetUserPassword")
            .WithTags("Admin", "Users")
            .WithSummary("Reset user password (admin only)")
            .WithDescription(@"Admin quick action to reset user password.

**Authorization**: Admin session required

**Request**: NewPassword (must meet security requirements)

**Behavior**:
- Resets user password
- Logs action in audit trail

**Issue**: #2890 - User Detail Modal/Page")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // Send email to user (admin only) - Issue #2890
        group.MapPost("/admin/users/{userId:guid}/send-email", HandleSendUserEmail)
            .RequireAdminSession()
            .WithName("SendUserEmail")
            .WithTags("Admin", "Users")
            .WithSummary("Send email to user (admin only)")
            .WithDescription(@"Admin quick action to send custom email to user.

**Authorization**: Admin session required

**Request**: Subject, Body

**Behavior**:
- Sends email to user's registered email address
- Logs action in audit trail

**Note**: Email service integration pending - currently logs only

**Issue**: #2890 - User Detail Modal/Page")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapUserImpersonateEndpoint(RouteGroupBuilder group)
    {
        // Impersonate user (admin only) - Issue #2890
        group.MapPost("/admin/users/{userId:guid}/impersonate", HandleImpersonateUser)
            .RequireAdminSession()
            .WithName("ImpersonateUser")
            .WithTags("Admin", "Users", "Debug")
            .WithSummary("Impersonate user for debugging (admin only)")
            .WithDescription(@"Create session as another user for debugging purposes.

**Authorization**: Admin session required

**Security**:
- ⚠️ HIGH RISK: Creates full user session
- Logs impersonation in audit trail
- Session marked as impersonated
- Limited duration (24 hours)

**Behavior**:
- Creates new session for target user
- Returns session token
- Original admin session remains active
- Audit log records both admin and impersonated user

**Use Case**: Debug user-specific issues, test permissions

**Issue**: #2890 - User Detail Modal/Page")
            .Produces<ImpersonateUserResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapEndImpersonationEndpoint(RouteGroupBuilder group)
    {
        // End impersonation session (admin only) - Issue #3349
        group.MapPost("/admin/impersonation/end", HandleEndImpersonation)
            .RequireAdminSession()
            .WithName("EndImpersonation")
            .WithTags("Admin", "Users", "Debug")
            .WithSummary("End an impersonation session (admin only)")
            .WithDescription(@"Terminates an active impersonation session.

**Authorization**: Admin session required

**Security**:
- Revokes the impersonation session
- Logs end of impersonation in audit trail
- Admin returns to their original session

**Issue**: #3349 - User Impersonation for Support/Debug")
            .Produces<EndImpersonationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> HandleBulkPasswordReset(
        BulkPasswordResetRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} initiating bulk password reset for {Count} users",
            session!.User!.Id, request.UserIds.Count);

        var command = new BulkPasswordResetCommand(
            request.UserIds,
            request.NewPassword,
            session.User!.Id
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleBulkRoleChange(
        BulkRoleChangeRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireSuperAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} initiating bulk role change for {Count} users to role {Role}",
            session!.User!.Id, request.UserIds.Count, request.NewRole);

        var command = new BulkRoleChangeCommand(
            request.UserIds,
            request.NewRole,
            session.User!.Id
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleBulkImportUsers(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        // Read CSV from request body
        using var reader = new StreamReader(context.Request.Body);
        var csvContent = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} initiating bulk user import from CSV",
            session!.User!.Id);

        var command = new BulkImportUsersCommand(
            csvContent,
            session.User!.Id
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleBulkExportUsers(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct,
        string? role = null,
        string? search = null)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} exporting users to CSV with filters: Role={Role}, Search={Search}",
            session!.User!.Id, role, search);

        var query = new BulkExportUsersQuery(role, search);
        var csv = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Content(csv, "text/csv", System.Text.Encoding.UTF8);
    }

    private static async Task<IResult> HandleSuspendUser(
        string id,
        SuspendUserRequest? request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} suspending user {UserId}", session!.User!.Id, id);

        // Issue #2886: Get admin ID for audit logging (session.User.Id is already Guid)
        var requesterId = session.User.Id;

        try
        {
            var command = new SuspendUserCommand(id, requesterId, request?.Reason);
            var user = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} suspended successfully by admin {AdminId}", id, requesterId);
            return Results.Ok(user);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error suspending user {UserId}", id);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    private static async Task<IResult> HandleUnsuspendUser(
        string id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} unsuspending user {UserId}", session!.User!.Id, id);

        // Issue #2886: Get admin ID for audit logging (session.User.Id is already Guid)
        var requesterId = session.User.Id;

        try
        {
            var command = new UnsuspendUserCommand(id, requesterId);
            var user = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} unsuspended successfully by admin {AdminId}", id, requesterId);
            return Results.Ok(user);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error unsuspending user {UserId}", id);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    // ISSUE-3339: Unlock account handler
    private static async Task<IResult> HandleUnlockAccount(
        string id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        if (!Guid.TryParse(id, out var userId))
        {
            return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
        }

        logger.LogInformation("Admin {AdminId} unlocking account {UserId}", session!.User!.Id, userId);

        try
        {
            var command = new UnlockAccountCommand(userId, session.User.Id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Account {UserId} unlocked successfully by admin {AdminId}", userId, session.User.Id);
            return Results.Ok(result);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "User {UserId} not found for unlock", userId);
            return Results.NotFound(new { error = "User not found" });
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error unlocking account {UserId}", userId);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    // ISSUE-3339: Get lockout status handler
    private static async Task<IResult> HandleGetLockoutStatus(
        string id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        if (!Guid.TryParse(id, out var userId))
        {
            return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
        }

        logger.LogInformation("Admin {AdminId} checking lockout status for user {UserId}", session!.User!.Id, userId);

        try
        {
            var query = new GetAccountLockoutStatusQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "User {UserId} not found for lockout status", userId);
            return Results.NotFound(new { error = "User not found" });
        }
    }

    private static async Task<IResult> HandleGetUserActivity(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct,
        string? actionFilter = null,
        string? resourceFilter = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} fetching activity for user {UserId}", session!.User!.Id, userId);

        var query = new Api.BoundedContexts.Administration.Application.Queries.GetUserActivityQuery(
            UserId: userId,
            ActionFilter: actionFilter,
            ResourceFilter: resourceFilter,
            StartDate: startDate,
            EndDate: endDate,
            Limit: limit
        );

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        logger.LogInformation("Activity timeline retrieved for user {UserId}: {Count} activities", userId, result.Activities.Count);

        return Results.Json(result);
    }

    private static async Task<IResult> HandleGetUserLibraryStats(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} retrieving library stats for user {UserId}", session!.User!.Id, userId);

        var query = new GetUserLibraryStatsQuery(userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        if (result is null)
        {
            logger.LogInformation("User {UserId} has no library entries", userId);
            return Results.NotFound(new { error = "User has no library entries or user does not exist" });
        }

        logger.LogInformation("Admin {AdminId} retrieved library stats for user {UserId}: {TotalGames} games, {SessionsPlayed} sessions",
            session.User.Id, userId, result.TotalGames, result.SessionsPlayed);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleResetUserPassword(
        Guid userId,
        ResetUserPasswordRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} resetting password for user {UserId}",
            session!.User!.Id, userId);

        try
        {
            var command = new ResetUserPasswordCommand(userId.ToString(), request.NewPassword);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Password reset successful for user {UserId} by admin {AdminId}",
                userId, session.User.Id);

            return Results.Ok(new { message = "Password reset successful" });
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "User {UserId} not found for password reset", userId);
            return Results.NotFound(new { error = "User not found" });
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error resetting password for user {UserId}", userId);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    private static async Task<IResult> HandleSendUserEmail(
        Guid userId,
        SendUserEmailRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} sending email to user {UserId}",
            session!.User!.Id, userId);

        try
        {
            var command = new SendUserEmailCommand(
                userId,
                request.Subject,
                request.Body,
                session.User.Id);

            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Email sent to user {UserId} by admin {AdminId}",
                userId, session.User.Id);

            return Results.Ok(new { message = "Email sent successfully" });
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "User {UserId} not found for email sending", userId);
            return Results.NotFound(new { error = "User not found" });
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error sending email to user {UserId}", userId);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    private static async Task<IResult> HandleImpersonateUser(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogWarning("⚠️ Admin {AdminId} attempting to impersonate user {UserId}",
            session!.User!.Id, userId);

        try
        {
            var command = new ImpersonateUserCommand(
                userId,
                session.User.Id);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogWarning("⚠️ Impersonation successful: Admin {AdminId} → User {UserId}",
                session.User.Id, userId);

            return Results.Ok(result);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "User {UserId} not found for impersonation", userId);
            return Results.NotFound(new { error = "User not found" });
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error impersonating user {UserId}", userId);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    private static async Task<IResult> HandleEndImpersonation(
        EndImpersonationRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogWarning("⚠️ Admin {AdminId} ending impersonation session {SessionId}",
            session!.User!.Id, request.SessionId);

        var command = new EndImpersonationCommand(
            request.SessionId,
            session.User.Id);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (result)
        {
            logger.LogWarning("⚠️ Impersonation ended successfully by Admin {AdminId}",
                session.User.Id);
            return Results.Ok(new EndImpersonationResponse(true, "Impersonation ended successfully"));
        }

        return Results.BadRequest(new EndImpersonationResponse(false, "Failed to end impersonation"));
    }
}
