using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// REST endpoints for the AgentMemory bounded context.
/// Provides group memory, game memory (house rules/notes), and player stats management.
/// </summary>
internal static class AgentMemoryEndpoints
{
    public static RouteGroupBuilder MapAgentMemoryEndpoints(this RouteGroupBuilder group)
    {
        var agentMemory = group.MapGroup("/agent-memory")
            .WithTags("AgentMemory");

        // === Group Memory ===

        agentMemory.MapPost("/groups", HandleCreateGroup)
            .RequireAuthenticatedUser()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(401)
            .WithSummary("Create a named play group");

        agentMemory.MapGet("/groups/{groupId:guid}", HandleGetGroup)
            .RequireAuthenticatedUser()
            .Produces<GroupMemoryDto>(200)
            .Produces(404)
            .Produces(401)
            .WithSummary("Get group memory details");

        agentMemory.MapPut("/groups/{groupId:guid}/preferences", HandleUpdatePreferences)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .Produces(401)
            .WithSummary("Update group preferences");

        // === Game Memory ===

        agentMemory.MapGet("/games/{gameId:guid}/memory", HandleGetGameMemory)
            .RequireAuthenticatedUser()
            .Produces<GameMemoryDto>(200)
            .Produces(404)
            .Produces(401)
            .WithSummary("Get game memory (house rules, notes)");

        agentMemory.MapPost("/games/{gameId:guid}/memory/house-rules", HandleAddHouseRule)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .WithSummary("Add a house rule");

        agentMemory.MapPost("/games/{gameId:guid}/memory/notes", HandleAddNote)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .WithSummary("Add a memory note");

        // === Player Stats ===

        agentMemory.MapGet("/players/me/stats", HandleGetMyStats)
            .RequireAuthenticatedUser()
            .Produces<List<PlayerMemoryDto>>(200)
            .Produces(401)
            .WithSummary("Get my player stats across all games");

        agentMemory.MapGet("/players/me/claimable-guests", HandleGetClaimableGuests)
            .RequireAuthenticatedUser()
            .Produces<List<ClaimableGuestDto>>(200)
            .Produces(400)
            .Produces(401)
            .WithSummary("Find claimable guest identities");

        agentMemory.MapPost("/players/me/claim-guest", HandleClaimGuest)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .WithSummary("Claim a guest player identity");

        return group;
    }

    // === Group Handlers ===

    private static async Task<IResult> HandleCreateGroup(
        [FromBody] CreateGroupRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new CreateGroupMemoryCommand(
            userId, request.Name, request.InitialMemberUserIds, request.InitialGuestNames);

        var groupId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/agent-memory/groups/{groupId}", groupId);
    }

    private static async Task<IResult> HandleGetGroup(
        Guid groupId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGroupMemoryQuery(groupId), cancellationToken).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdatePreferences(
        Guid groupId,
        [FromBody] UpdatePreferencesRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateGroupPreferencesCommand(
            groupId, request.MaxDuration, request.PreferredComplexity, request.CustomNotes);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    // === Game Memory Handlers ===

    private static async Task<IResult> HandleGetGameMemory(
        Guid gameId,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var result = await mediator.Send(new GetGameMemoryQuery(gameId, userId), cancellationToken).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> HandleAddHouseRule(
        Guid gameId,
        [FromBody] AddHouseRuleRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new AddHouseRuleCommand(gameId, userId, request.Description);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleAddNote(
        Guid gameId,
        [FromBody] AddNoteRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new AddMemoryNoteCommand(gameId, userId, request.Content);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    // === Player Stats Handlers ===

    private static async Task<IResult> HandleGetMyStats(
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var result = await mediator.Send(new GetPlayerStatsQuery(userId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetClaimableGuests(
        [FromQuery] string guestName,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var result = await mediator.Send(new GetClaimableGuestsQuery(userId, guestName), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleClaimGuest(
        [FromBody] ClaimGuestRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new ClaimGuestPlayerCommand(userId, request.PlayerMemoryId);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    // === Request DTOs ===

    internal record CreateGroupRequest(
        string Name,
        List<Guid>? InitialMemberUserIds = null,
        List<string>? InitialGuestNames = null);

    internal record UpdatePreferencesRequest(
        TimeSpan? MaxDuration,
        string? PreferredComplexity,
        string? CustomNotes);

    internal record AddHouseRuleRequest(string Description);

    internal record AddNoteRequest(string Content);

    internal record ClaimGuestRequest(Guid PlayerMemoryId);
}
