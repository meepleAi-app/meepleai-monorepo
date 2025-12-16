using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
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
        // User search endpoint (authenticated users)
        group.MapGet("/users/search", async (
            string query,
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("User {UserId} searching for users with query: {Query}", session!.User!.Id, query);

            // Use CQRS Query for user search
            var searchQuery = new SearchUsersQuery(query, MaxResults: 10);
            var users = await mediator.Send(searchQuery, ct).ConfigureAwait(false);

            return Results.Ok(users);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .WithName("SearchUsers")
        .WithTags("Users")
        .WithDescription("Search users by display name or email for @mention autocomplete (max 10 results)")
        .Produces<IEnumerable<UserSearchResultDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // ADMIN-01: User management endpoints
        group.MapGet("/admin/users", async (
            HttpContext context,
            IMediator mediator,
            string? search = null,
            string? role = null,
            string? sortBy = null,
            string? sortOrder = "desc",
            int page = 1,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAllUsersQuery(search, role, sortBy, sortOrder, page, limit);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(result);
        })
        .WithName("GetUsers")
        .WithTags("Admin");

        group.MapPost("/admin/users", async (
            CreateUserRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} creating new user with email {Email}", session!.User!.Id, request.Email);
            var command = new CreateUserCommand(request.Email, request.Password, request.DisplayName, request.Role ?? "user");
            var user = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} created successfully", user.Id);
            return Results.Created($"/api/v1/admin/users/{user.Id}", user);
        })
        .WithName("CreateUser")
        .WithTags("Admin");

        group.MapPut("/admin/users/{id}", async (
            string id,
            UpdateUserRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} updating user {UserId}", session!.User!.Id, id);
            var command = new UpdateUserCommand(id, request.Email, request.DisplayName, request.Role);
            var user = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} updated successfully", id);
            return Results.Ok(user);
        })
        .WithName("UpdateUser")
        .WithTags("Admin");

        group.MapDelete("/admin/users/{id}", async (
            string id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} deleting user {UserId}", session!.User!.Id, id);
            var command = new DeleteUserCommand(id, session.User!.Id.ToString());
            await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} deleted successfully", id);
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .WithTags("Admin");

        // ADMIN-TIER-01: Update user subscription tier
        group.MapPut("/admin/users/{id}/tier", async (
            string id,
            UpdateUserTierRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
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
                !ValidTiers.Contains(request.Tier.ToLowerInvariant(), StringComparer.Ordinal))
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
        })
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

        // BULK OPERATIONS - Issue #905
        group.MapPost("/admin/users/bulk/password-reset", async (
            BulkPasswordResetRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
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
        })
        .WithName("BulkPasswordReset")
        .WithTags("Admin")
        .WithSummary("Reset passwords for multiple users")
        .Produces<BulkOperationResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/admin/users/bulk/role-change", async (
            BulkRoleChangeRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
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
        })
        .WithName("BulkRoleChange")
        .WithTags("Admin")
        .WithSummary("Change role for multiple users")
        .Produces<BulkOperationResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/admin/users/bulk/import", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
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
        })
        .WithName("BulkImportUsers")
        .WithTags("Admin")
        .WithSummary("Import users from CSV file")
        .WithDescription("CSV format: email,displayName,role,password")
        .Accepts<string>("text/csv")
        .Produces<BulkOperationResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/admin/users/bulk/export", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            string? role = null,
            string? search = null,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} exporting users to CSV with filters: Role={Role}, Search={Search}",
                session!.User!.Id, role, search);

            var query = new BulkExportUsersQuery(role, search);
            var csv = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Content(csv, "text/csv", System.Text.Encoding.UTF8);
        })
        .WithName("BulkExportUsers")
        .WithTags("Admin")
        .WithSummary("Export users to CSV file")
        .WithDescription("Returns CSV with format: email,displayName,role,createdAt")
        .Produces<string>(StatusCodes.Status200OK, "text/csv")
        .Produces(StatusCodes.Status401Unauthorized);

        // Get user activity timeline (ADMIN-USER-ACTIVITY-01 - Issue #911)
        group.MapGet("/admin/users/{userId}/activity", async (
            Guid userId,
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
        })
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

        return group;
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
