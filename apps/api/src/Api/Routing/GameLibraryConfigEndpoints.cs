using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Game library tier limits configuration endpoints (Admin only).
/// Issue #2444: Admin UI - Configure Game Library Tier Limits
/// </summary>
internal static class GameLibraryConfigEndpoints
{
    public static RouteGroupBuilder MapGameLibraryConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/config/game-library-limits", HandleGetGameLibraryLimits)
            .WithName("GetGameLibraryLimits")
            .WithTags("Admin", "GameLibrary", "Configuration")
            .WithSummary("Get current game library tier limits")
            .WithDescription("Retrieves the maximum game counts for Free, Normal, and Premium tiers")
            .Produces<GameLibraryLimitsDto>();

        group.MapPut("/admin/config/game-library-limits", HandleUpdateGameLibraryLimits)
            .WithName("UpdateGameLibraryLimits")
            .WithTags("Admin", "GameLibrary", "Configuration")
            .WithSummary("Update game library tier limits")
            .WithDescription("Updates the maximum game counts for all tiers. Requires admin role.")
            .Produces<GameLibraryLimitsDto>()
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<IResult> HandleGetGameLibraryLimits(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Admin authorization check
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetGameLibraryLimitsQuery();
        var limits = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(limits);
    }

    private static async Task<IResult> HandleUpdateGameLibraryLimits(
        UpdateGameLibraryLimitsRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Admin authorization check with user ID extraction
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation(
            "Admin {UserId} updating game library limits: Free={Free}, Normal={Normal}, Premium={Premium}",
            userId,
            request.FreeTierLimit,
            request.NormalTierLimit,
            request.PremiumTierLimit);

        var command = new UpdateGameLibraryLimitsCommand(
            FreeTierLimit: request.FreeTierLimit,
            NormalTierLimit: request.NormalTierLimit,
            PremiumTierLimit: request.PremiumTierLimit,
            UpdatedByUserId: userId
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {UserId} successfully updated game library limits",
            userId);

        return Results.Ok(result);
    }
}
