using Api.BoundedContexts.SystemConfiguration.Application.Commands.CreateUserRateLimitOverride;
using Api.BoundedContexts.SystemConfiguration.Application.Commands.RemoveUserRateLimitOverride;
using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateRateLimitConfig;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries.GetAllRateLimitOverrides;
using Api.BoundedContexts.SystemConfiguration.Application.Queries.GetRateLimitConfig;
using Api.BoundedContexts.SystemConfiguration.Application.Queries.GetUserRateLimitStatus;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Rate limit admin management endpoints.
/// Issue #2738 - Phase 4bis: API Extensions
/// Provides admin control over tier-based rate limit configurations and user-specific overrides.
/// </summary>
internal static class RateLimitAdminEndpoints
{
    public static RouteGroupBuilder MapRateLimitAdminEndpoints(this RouteGroupBuilder group)
    {
        MapRateLimitConfigEndpoints(group);
        MapRateLimitOverrideEndpoints(group);

        return group;
    }

    private static void MapRateLimitConfigEndpoints(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/config/share-request-limits
        group.MapGet("/admin/config/share-request-limits", HandleGetRateLimitConfigs)
            .WithName("GetRateLimitConfigs")
            .WithTags("Admin", "Configuration")
            .Produces<IReadOnlyList<RateLimitConfigDto>>();

        // PUT /api/v1/admin/config/share-request-limits/{tier}
        group.MapPut("/admin/config/share-request-limits/{tier}", HandleUpdateRateLimitConfig)
            .WithName("UpdateRateLimitConfig")
            .WithTags("Admin", "Configuration")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem();
    }

    private static void MapRateLimitOverrideEndpoints(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/users/{id}/rate-limit-status
        group.MapGet("/admin/users/{id:guid}/rate-limit-status", HandleGetUserRateLimitStatus)
            .WithName("GetUserRateLimitStatus")
            .WithTags("Admin", "Users")
            .Produces<UserRateLimitStatusDto>()
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/users/{id}/rate-limit-override
        group.MapPost("/admin/users/{id:guid}/rate-limit-override", HandleCreateRateLimitOverride)
            .WithName("CreateRateLimitOverride")
            .WithTags("Admin", "Users")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        // DELETE /api/v1/admin/users/{id}/rate-limit-override
        group.MapDelete("/admin/users/{id:guid}/rate-limit-override", HandleRemoveRateLimitOverride)
            .WithName("RemoveRateLimitOverride")
            .WithTags("Admin", "Users")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/rate-limit-overrides
        group.MapGet("/admin/rate-limit-overrides", HandleGetAllRateLimitOverrides)
            .WithName("GetAllRateLimitOverrides")
            .WithTags("Admin", "Configuration")
            .Produces<PagedRateLimitOverridesResult>();
    }

    /// <summary>
    /// Get all tier-based rate limit configurations.
    /// </summary>
    private static async Task<IResult> HandleGetRateLimitConfigs(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        bool activeOnly = true)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetRateLimitConfigQuery { ActiveOnly = activeOnly };
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(result);
    }

    /// <summary>
    /// Update rate limit configuration for a specific tier.
    /// </summary>
    private static async Task<IResult> HandleUpdateRateLimitConfig(
        UserTier tier,
        UpdateRateLimitConfigRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} updating rate limit config for tier {Tier}",
            session!.User!.Id, tier);

        var command = new UpdateRateLimitConfigCommand
        {
            AdminId = session!.User!.Id,
            Tier = tier,
            MaxPendingRequests = request.MaxPendingRequests,
            MaxRequestsPerMonth = request.MaxRequestsPerMonth,
            CooldownAfterRejection = TimeSpan.FromDays(request.CooldownDays)
        };

        await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Rate limit config for tier {Tier} updated successfully", tier);

        return Results.NoContent();
    }

    /// <summary>
    /// Get complete rate limit status for a specific user.
    /// </summary>
    private static async Task<IResult> HandleGetUserRateLimitStatus(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetUserRateLimitStatusQuery { UserId = id };
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result != null ? Results.Json(result) : Results.NotFound(new { error = "User not found or has no rate limit status" });
    }

    /// <summary>
    /// Create a user-specific rate limit override.
    /// </summary>
    private static async Task<IResult> HandleCreateRateLimitOverride(
        Guid id,
        CreateRateLimitOverrideRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} creating rate limit override for user {UserId}",
            session!.User!.Id, id);

        var command = new CreateUserRateLimitOverrideCommand
        {
            AdminId = session!.User!.Id,
            UserId = id,
            MaxPendingRequests = request.MaxPendingRequests,
            MaxRequestsPerMonth = request.MaxRequestsPerMonth,
            CooldownAfterRejection = request.CooldownDays.HasValue
                ? TimeSpan.FromDays(request.CooldownDays.Value)
                : null,
            ExpiresAt = request.ExpiresAt,
            Reason = request.Reason
        };

        await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Rate limit override created for user {UserId}", id);

        return Results.Created($"/api/v1/admin/users/{id}/rate-limit-override", new { userId = id });
    }

    /// <summary>
    /// Remove user-specific rate limit override.
    /// </summary>
    private static async Task<IResult> HandleRemoveRateLimitOverride(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} removing rate limit override for user {UserId}",
            session!.User!.Id, id);

        var command = new RemoveUserRateLimitOverrideCommand
        {
            AdminId = session!.User!.Id,
            UserId = id
        };

        await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Rate limit override removed for user {UserId}", id);

        return Results.NoContent();
    }

    /// <summary>
    /// List all rate limit overrides with pagination.
    /// </summary>
    private static async Task<IResult> HandleGetAllRateLimitOverrides(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        [FromQuery] bool includeExpired = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllRateLimitOverridesQuery
        {
            IncludeExpired = includeExpired,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(result);
    }

    /// <summary>
    /// Request DTO for updating rate limit configuration.
    /// </summary>
    private record UpdateRateLimitConfigRequest(
        int MaxPendingRequests,
        int MaxRequestsPerMonth,
        int CooldownDays);

    /// <summary>
    /// Request DTO for creating rate limit override.
    /// </summary>
    private record CreateRateLimitOverrideRequest(
        int? MaxPendingRequests,
        int? MaxRequestsPerMonth,
        int? CooldownDays,
        DateTime? ExpiresAt,
        string Reason);
}
