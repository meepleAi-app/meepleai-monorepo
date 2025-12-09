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
public static class AdminUserEndpoints
{
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
            var validTiers = new[] { "free", "normal", "premium" };
            if (string.IsNullOrWhiteSpace(request.Tier) ||
                !validTiers.Contains(request.Tier.ToLowerInvariant(), StringComparer.Ordinal))
            {
                logger.LogWarning("Admin {AdminId} attempted to set invalid tier: {Tier}",
                    requesterId, request.Tier);
                return Results.BadRequest(new
                {
                    error = "invalid_tier",
                    message = $"Invalid tier value. Valid tiers are: {string.Join(", ", validTiers)}"
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

        return group;
    }
}

/// <summary>
/// Request payload for updating user tier.
/// </summary>
public record UpdateUserTierRequest(string Tier);