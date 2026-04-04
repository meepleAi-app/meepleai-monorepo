using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.Commands.AccountLockout;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Routing;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Endpoints and DTOs in same file
namespace Api.Routing.Admin;

internal static class AdminUserBulkEndpoints
{
    public static void MapBulkEndpoints(RouteGroupBuilder group)
    {
        MapBulkUserEndpoints(group);
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
        var (authorized, session, error) = context.RequireAdminSession();
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
}
