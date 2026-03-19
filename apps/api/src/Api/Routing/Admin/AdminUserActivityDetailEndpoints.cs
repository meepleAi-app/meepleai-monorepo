using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Routing;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Endpoints and DTOs in same file
namespace Api.Routing.Admin;

internal static class AdminUserActivityDetailEndpoints
{
    public static void MapActivityDetailEndpoints(RouteGroupBuilder group)
    {
        MapUserActivityEndpoints(group);
        MapUserDetailEndpoint(group);
        MapUserRoleHistoryEndpoint(group);
        MapUserQuickActionsEndpoints(group);
        MapUserRoleChangeEndpoint(group);
        MapUserImpersonateEndpoint(group);
        MapEndImpersonationEndpoint(group);
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

    private static void MapUserDetailEndpoint(RouteGroupBuilder group)
    {
        // Get complete user details (admin only) - Issue #2890
        group.MapGet("/admin/users/{userId:guid}", HandleGetUserDetail)
            .RequireAdminSession()
            .WithName("GetUserDetail")
            .WithTags("Admin", "Users")
            .WithSummary("Get complete user details (admin only)")
            .WithDescription(@"Retrieve complete user information for admin User Detail page.

**Authorization**: Admin session required

**Response**: Complete user details including:
- Basic info: Id, Email, DisplayName, Role, Tier
- Gamification: Level, ExperiencePoints
- Status: EmailVerified, IsSuspended, IsTwoFactorEnabled
- Timestamps: CreatedAt, EmailVerifiedAt, SuspendedAt, TwoFactorEnabledAt

**Issue**: #2890 - User Detail Modal/Page")
            .Produces<Api.BoundedContexts.Authentication.Application.DTOs.UserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapUserRoleHistoryEndpoint(RouteGroupBuilder group)
    {
        // Get user role change history (admin only) - Issue #2890
        group.MapGet("/admin/users/{userId:guid}/role-history", HandleGetUserRoleHistory)
            .RequireAdminSession()
            .WithName("GetUserRoleHistory")
            .WithTags("Admin", "Users")
            .WithSummary("Get user role change history (admin only)")
            .WithDescription(@"Retrieve chronological history of role changes for a user.

**Authorization**: Admin session required

**Response**: List of role changes with:
- Timestamp
- Old role → New role
- Changed by (admin user)
- IP address

**Issue**: #2890 - User Detail Modal/Page")
            .Produces<List<RoleChangeHistoryDto>>(StatusCodes.Status200OK)
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

    private static void MapUserRoleChangeEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/admin/users/{userId:guid}/role", HandleChangeUserRole)
            .RequireAdminSession()
            .WithName("ChangeUserRole")
            .WithTags("Admin", "Users")
            .WithSummary("Change a single user's role")
            .WithDescription(@"Change a user's role. Automatically audited via [AuditableAction].

**Authorization**: Admin session required

**Valid Roles**: Admin, Editor, User

**Optional**: Reason field (max 500 chars) for audit trail

**Issue**: #124 - Admin Infrastructure Panel")
            .Produces<Api.Models.UserDto>(StatusCodes.Status200OK)
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

    private static async Task<IResult> HandleGetUserDetail(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} retrieving details for user {UserId}",
            session!.User!.Id, userId);

        var query = new Api.BoundedContexts.Authentication.Application.Queries.GetUserByIdQuery(userId);
        var user = await mediator.Send(query, ct).ConfigureAwait(false);

        if (user is null)
        {
            logger.LogWarning("User {UserId} not found", userId);
            return Results.NotFound(new { error = "User not found" });
        }

        logger.LogInformation("Admin {AdminId} retrieved details for user {UserId}",
            session.User.Id, userId);

        return Results.Ok(user);
    }

    private static async Task<IResult> HandleGetUserRoleHistory(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} retrieving role history for user {UserId}",
            session!.User!.Id, userId);

        var query = new GetUserRoleHistoryQuery(userId);
        var history = await mediator.Send(query, ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} retrieved {Count} role changes for user {UserId}",
            session.User.Id, history.Count, userId);

        return Results.Ok(history);
    }

    private static async Task<IResult> HandleChangeUserRole(
        Guid userId,
        ChangeUserRoleRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} changing role for user {UserId} to {NewRole}, reason: {Reason}",
            session!.User!.Id, userId, request.NewRole, request.Reason ?? "(none)");

        try
        {
            var command = new ChangeUserRoleCommand(userId.ToString(), request.NewRole, request.Reason);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Role changed for user {UserId} to {NewRole} by admin {AdminId}",
                userId, request.NewRole, session.User.Id);

            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Failed to change role for user {UserId}", userId);
            return Results.BadRequest(new { error = ex.Message });
        }
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
