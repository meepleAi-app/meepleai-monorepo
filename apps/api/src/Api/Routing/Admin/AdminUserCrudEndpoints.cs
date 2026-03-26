using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure.Security;
using Api.Models;
using Api.SharedKernel.Application.DTOs;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin user CRUD, search, and detail endpoints.
/// Covers user listing, creation, update, deletion, search, and full detail retrieval.
/// </summary>
internal static class AdminUserCrudEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapUserSearchEndpoints(group);
        MapUserCrudEndpoints(group);
        MapUserDetailEndpoint(group);
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
        string? tier = null,        // Issue #3698: Filter by tier
        string? sortBy = null,
        string? sortOrder = "desc",
        int page = 1,
        int limit = 20)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllUsersQuery(search, role, status, tier, sortBy, sortOrder, page, limit);
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
}
