using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure.Security;
using Api.Models;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Endpoints and DTOs in same file
namespace Api.Routing;

/// <summary>
/// User management endpoints (Admin only).
/// Handles user CRUD operations and user search functionality.
/// </summary>
internal static class AdminUserEndpoints
{
    private static readonly string[] ValidTiers = { "free", "normal", "premium" };

    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        MapUserSearchEndpoints(group);
        // ADMIN-01: User management endpoints
        MapUserCrudEndpoints(group);
        // ADMIN-TIER-01: Update user subscription tier
        MapUserTierEndpoints(group);
        // BULK OPERATIONS - Issue #905
        MapBulkUserEndpoints(group);
        // Get user activity timeline (ADMIN-USER-ACTIVITY-01 - Issue #911)
        MapUserActivityEndpoints(group);

        return group;
    }

    private static void MapUserSearchEndpoints(RouteGroupBuilder group)
    {
        // User search endpoint (authenticated users)
        group.MapGet("/users/search", HandleSearchUsers)
        .RequireSession() // Issue #1446: Automatic session validation
        .WithName("SearchUsers")
        .WithTags("Users")
        .WithDescription("Search users by display name or email for @mention autocomplete (max 10 results)")
        .Produces<IEnumerable<UserSearchResultDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static void MapUserCrudEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/admin/users", HandleGetUsers)
        .WithName("GetUsers")
        .WithTags("Admin");

        group.MapPost("/admin/users", HandleCreateUser)
        .WithName("CreateUser")
        .WithTags("Admin");

        group.MapPut("/admin/users/{id}", HandleUpdateUser)
        .WithName("UpdateUser")
        .WithTags("Admin");

        group.MapDelete("/admin/users/{id}", HandleDeleteUser)
        .WithName("DeleteUser")
        .WithTags("Admin");
    }

    private static void MapUserTierEndpoints(RouteGroupBuilder group)
    {
        group.MapPut("/admin/users/{id}/tier", HandleUpdateUserTier)
        .WithName("UpdateUserTier")
        .WithTags("Admin")
        .WithSummary("Update user's subscription tier")
        .WithDescription(@"Updates a user's subscription tier (free/normal/premium).

**Valid Tiers**:
- **free**: 5 PDF/day, 20 PDF/week
- **normal**: 20 PDF/day, 100 PDF/week
- **premium**: 100 PDF/day, 500 PDF/week

**Authorization**: Admin only

**Request Body**: UpdateUserTierRequest with tier field")
        .Produces<Api.BoundedContexts.Authentication.Application.DTOs.UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
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

    private static async Task<IResult> HandleSearchUsers(
        string query,
        IMediator mediator,
        HttpContext context,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Session validated by RequireSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        logger.LogInformation("User {UserId} searching for users with query: {Query}", session!.User!.Id, query);

        // Use CQRS Query for user search
        var searchQuery = new SearchUsersQuery(query, MaxResults: 10);
        var users = await mediator.Send(searchQuery, ct).ConfigureAwait(false);

        return Results.Ok(users);
    }

    private static async Task<IResult> HandleGetUsers(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        string? search = null,
        string? role = null,
        string? status = null,
        string? sortBy = null,
        string? sortOrder = "desc",
        int page = 1,
        int limit = 20)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllUsersQuery(search, role, status, sortBy, sortOrder, page, limit);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Json(result);
    }

    private static async Task<IResult> HandleCreateUser(
        CreateUserRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} creating new user with email {Email}", session!.User!.Id, DataMasking.MaskEmail(request.Email));
        var command = new CreateUserCommand(request.Email, request.Password, request.DisplayName, request.Role ?? "user");
        var user = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("User {UserId} created successfully", user.Id);
        return Results.Created($"/api/v1/admin/users/{user.Id}", user);
    }

    private static async Task<IResult> HandleUpdateUser(
        string id,
        UpdateUserRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} updating user {UserId}", session!.User!.Id, id);
        var command = new UpdateUserCommand(id, request.Email, request.DisplayName, request.Role);
        var user = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("User {UserId} updated successfully", id);
        return Results.Ok(user);
    }

    private static async Task<IResult> HandleDeleteUser(
        string id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} deleting user {UserId}", session!.User!.Id, id);
        var command = new DeleteUserCommand(id, session.User!.Id.ToString());
        await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("User {UserId} deleted successfully", id);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateUserTier(
        string id,
        UpdateUserTierRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        // Validate user ID format
        if (!Guid.TryParse(id, out var userId))
        {
            logger.LogWarning("Admin {AdminId} attempted to update tier with invalid user ID: {UserId}",
                session!.User!.Id, id);
            return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
        }

        // Validate requester ID format
        if (!Guid.TryParse(session!.User!.Id.ToString(), out var requesterId))
        {
            logger.LogError("Invalid requester ID format in session: {RequesterId}", session.User!.Id);
            return Results.BadRequest(new { error = "invalid_session", message = "Invalid session user ID format" });
        }

        // Validate tier value
        if (string.IsNullOrWhiteSpace(request.Tier) ||
            !ValidTiers.Contains(request.Tier, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("Admin {AdminId} attempted to set invalid tier: {Tier}",
                requesterId, request.Tier);
            return Results.BadRequest(new
            {
                error = "invalid_tier",
                message = $"Invalid tier value. Valid tiers are: {string.Join(", ", ValidTiers)}"
            });
        }

        logger.LogInformation("Admin {AdminId} updating tier for user {UserId} to {NewTier}",
            requesterId, userId, request.Tier);

        try
        {
            var command = new UpdateUserTierCommand(userId, request.Tier, requesterId);
            var user = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} tier updated successfully to {Tier}", userId, request.Tier);
            return Results.Ok(user);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error updating tier for user {UserId}", userId);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
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
            Guid.Parse(session.User!.Id.ToString())
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
            Guid.Parse(session.User!.Id.ToString())
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
            Guid.Parse(session.User!.Id.ToString())
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

        try
        {
            var command = new SuspendUserCommand(id, request?.Reason);
            var user = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} suspended successfully", id);
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

        try
        {
            var command = new UnsuspendUserCommand(id);
            var user = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} unsuspended successfully", id);
            return Results.Ok(user);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error unsuspending user {UserId}", id);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
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
}

/// <summary>
/// Request payload for updating user tier.
/// </summary>
internal record UpdateUserTierRequest(string Tier);

/// <summary>
/// Request payload for bulk password reset.
/// </summary>
internal record BulkPasswordResetRequest(IReadOnlyList<Guid> UserIds, string NewPassword);

/// <summary>
/// Request payload for bulk role change.
/// </summary>
internal record BulkRoleChangeRequest(IReadOnlyList<Guid> UserIds, string NewRole);

/// <summary>
/// Request payload for suspending a user account.
/// </summary>
internal record SuspendUserRequest(string? Reason);
