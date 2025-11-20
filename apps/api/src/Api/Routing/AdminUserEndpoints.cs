using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using Api.Models;
using MediatR;

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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation("User {UserId} searching for users with query: {Query}", session.User.Id, query);

            // Use CQRS Query for user search
            var searchQuery = new SearchUsersQuery(query, MaxResults: 10);
            var users = await mediator.Send(searchQuery, ct);

            return Results.Ok(users);
        })
        .RequireAuthorization()
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
            var result = await mediator.Send(query, ct);
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

            logger.LogInformation("Admin {AdminId} creating new user with email {Email}", session.User.Id, request.Email);
            var command = new CreateUserCommand(request.Email, request.Password, request.DisplayName, request.Role ?? "user");
            var user = await mediator.Send(command, ct);
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

            logger.LogInformation("Admin {AdminId} updating user {UserId}", session.User.Id, id);
            var command = new UpdateUserCommand(id, request.Email, request.DisplayName, request.Role);
            var user = await mediator.Send(command, ct);
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

            logger.LogInformation("Admin {AdminId} deleting user {UserId}", session.User.Id, id);
            var command = new DeleteUserCommand(id, session.User.Id);
            await mediator.Send(command, ct);
            logger.LogInformation("User {UserId} deleted successfully", id);
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .WithTags("Admin");

        return group;
    }
}
