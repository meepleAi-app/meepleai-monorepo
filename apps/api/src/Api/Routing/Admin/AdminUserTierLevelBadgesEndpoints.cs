using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;
using Api.Extensions;
using Api.Infrastructure.Security;
using Api.Routing;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Endpoints and DTOs in same file
namespace Api.Routing.Admin;

internal static class AdminUserTierLevelBadgesEndpoints
{
    private static readonly string[] ValidTiers = { "free", "normal", "premium" };

    public static void MapTierLevelBadgesEndpoints(RouteGroupBuilder group)
    {
        MapUserTierEndpoints(group);
        MapUserLevelEndpoints(group);
        MapUserLibraryStatsEndpoints(group);
        MapUserBadgesEndpoints(group);
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

    private static void MapUserLevelEndpoints(RouteGroupBuilder group)
    {
        // Set user level (admin only) - Issue #3141
        group.MapPatch("/admin/users/{userId:guid}/level", HandleSetUserLevel)
            .WithName("SetUserLevel")
            .WithTags("Admin", "Users")
            .WithDescription("Set user level (admin only)")
            .Produces<Api.BoundedContexts.Authentication.Application.DTOs.UserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);
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

    private static void MapUserBadgesEndpoints(RouteGroupBuilder group)
    {
        // Get user badges (admin only) - Issue #3140
        group.MapGet("/admin/users/{userId:guid}/badges", HandleGetUserBadges)
            .RequireAdminSession()
            .WithName("GetUserBadgesAdmin")
            .WithTags("Admin", "Badges")
            .WithSummary("Get all user badges (admin only)")
            .WithDescription(@"Retrieve all badges earned by a specific user, including both visible and hidden badges.

**Authorization**: Admin session required (403 for non-admins)

**Behavior**:
- Returns all badges for the user (visible + hidden)
- Excludes revoked badges
- Returns empty array `[]` if user has no badges
- Returns empty array `[]` if user does not exist (no 404)

**Ordering**: Badges ordered by DisplayOrder, then EarnedAt descending

**Example Response**:
```json
[
  {
    ""id"": ""badge-guid-1"",
    ""code"": ""FIRST_GAME"",
    ""name"": ""First Game"",
    ""description"": ""Added first game to library"",
    ""iconUrl"": ""/badges/first-game.png"",
    ""tier"": ""Bronze"",
    ""earnedAt"": ""2026-01-15T10:30:00Z"",
    ""isDisplayed"": true
  },
  {
    ""id"": ""badge-guid-2"",
    ""code"": ""SECRET_BADGE"",
    ""name"": ""Secret Achievement"",
    ""description"": ""Hidden badge description"",
    ""iconUrl"": ""/badges/secret.png"",
    ""tier"": ""Gold"",
    ""earnedAt"": ""2026-01-20T14:00:00Z"",
    ""isDisplayed"": false
  }
]
```

**Issue**: #3140 - Admin endpoint to view user badges")
            .Produces<List<UserBadgeDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);
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

    private static async Task<IResult> HandleSetUserLevel(
        Guid userId,
        SetUserLevelRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} setting level for user {UserId} to {Level}",
            session!.User!.Id, userId, request.Level);

        var command = new SetUserLevelCommand(userId, request.Level);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} level set to {Level} by admin {AdminId}",
            userId, request.Level, session.User.Id);

        return Results.Ok(result);
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

    private static async Task<IResult> HandleGetUserBadges(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} retrieving badges for user {UserId}", session!.User!.Id, userId);

        // Reuse existing GetUserBadgesQuery with IncludeHidden: true
        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} retrieved {Count} badges for user {UserId}",
            session.User.Id, result.Count, userId);

        return Results.Ok(result);
    }
}
