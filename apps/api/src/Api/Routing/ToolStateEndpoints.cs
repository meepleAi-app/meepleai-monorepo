using Api.BoundedContexts.GameManagement.Application.Commands.ToolState;
using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Tool state endpoints for managing runtime tool states in live sessions.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal static class ToolStateEndpoints
{
    public static RouteGroupBuilder MapToolStateEndpoints(this RouteGroupBuilder group)
    {
        // === Commands ===

        group.MapPost("/live-sessions/{sessionId}/tool-states/initialize", HandleInitialize)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<ToolStateDto>>(201)
            .Produces(400)
            .Produces(404)
            .Produces(409)
            .WithTags("ToolStates")
            .WithSummary("Initialize tool states for a session")
            .WithDescription("Creates tool state entries for all tools in the specified toolkit.");

        group.MapPost("/live-sessions/{sessionId}/tool-states/{toolName}/roll", HandleRollDice)
            .RequireAuthenticatedUser()
            .Produces<ToolStateDto>()
            .Produces(400)
            .Produces(404)
            .WithTags("ToolStates")
            .WithSummary("Roll dice for a tool")
            .WithDescription("Generates random dice values and updates the tool state.");

        group.MapPost("/live-sessions/{sessionId}/tool-states/{toolName}/counter", HandleUpdateCounter)
            .RequireAuthenticatedUser()
            .Produces<ToolStateDto>()
            .Produces(400)
            .Produces(404)
            .WithTags("ToolStates")
            .WithSummary("Update counter value")
            .WithDescription("Applies a delta change to a counter tool value.");

        // === Queries ===

        group.MapGet("/live-sessions/{sessionId}/tool-states", HandleGetAll)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<ToolStateDto>>()
            .Produces(400)
            .WithTags("ToolStates")
            .WithSummary("Get all tool states for a session")
            .WithDescription("Returns all tool states associated with a live session.");

        group.MapGet("/live-sessions/{sessionId}/tool-states/{toolName}", HandleGetByName)
            .RequireAuthenticatedUser()
            .Produces<ToolStateDto>()
            .Produces(404)
            .WithTags("ToolStates")
            .WithSummary("Get a specific tool state")
            .WithDescription("Returns a single tool state by session and tool name.");

        return group;
    }

    private static async Task<IResult> HandleInitialize(
        Guid sessionId, InitializeToolStatesRequest request, IMediator mediator)
    {
        var command = new InitializeToolStatesCommand(sessionId, request.ToolkitId);
        var result = await mediator.Send(command).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}/tool-states", result);
    }

    private static async Task<IResult> HandleRollDice(
        Guid sessionId, string toolName, RollDiceRequest request, IMediator mediator)
    {
        var command = new RollDiceCommand(sessionId, toolName, request.PlayerId);
        var result = await mediator.Send(command).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateCounter(
        Guid sessionId, string toolName, UpdateCounterRequest request, IMediator mediator)
    {
        var command = new UpdateCounterCommand(sessionId, toolName, request.PlayerId, request.Change);
        var result = await mediator.Send(command).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAll(
        Guid sessionId, IMediator mediator)
    {
        var query = new GetToolStatesQuery(sessionId);
        var result = await mediator.Send(query).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetByName(
        Guid sessionId, string toolName, IMediator mediator)
    {
        var query = new GetToolStateQuery(sessionId, toolName);
        var result = await mediator.Send(query).ConfigureAwait(false);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }
}
