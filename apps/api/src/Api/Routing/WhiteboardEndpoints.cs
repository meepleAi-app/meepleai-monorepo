using System.Security.Claims;
using Api.BoundedContexts.GameManagement.Application.Commands.Whiteboard;
using Api.BoundedContexts.GameManagement.Application.DTOs.Whiteboard;
using Api.BoundedContexts.GameManagement.Application.Queries.Whiteboard;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Endpoints for collaborative whiteboard management within live sessions.
/// Supports freehand strokes (delta-based add/remove) and a structured layer (tokens, grid).
/// Part of the base toolkit — always available.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal static class WhiteboardEndpoints
{
    public static RouteGroupBuilder MapWhiteboardEndpoints(this RouteGroupBuilder group)
    {
        // === Commands ===

        group.MapPost("/live-sessions/{sessionId}/whiteboard/initialize", HandleInitialize)
            .RequireAuthenticatedUser()
            .Produces<WhiteboardStateDto>(201)
            .Produces(401)
            .Produces(404)
            .Produces(409)
            .WithTags("Whiteboard")
            .WithSummary("Initialize whiteboard for a session")
            .WithDescription("Creates an empty WhiteboardState for the session. Fails with 409 if already initialized. Issue #4971.");

        group.MapPost("/live-sessions/{sessionId}/whiteboard/strokes", HandleAddStroke)
            .RequireAuthenticatedUser()
            .Produces<WhiteboardStateDto>()
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .Produces(409)
            .WithTags("Whiteboard")
            .WithSummary("Add a freehand stroke")
            .WithDescription("Appends a new freehand stroke to the whiteboard. Broadcasts stroke-added SSE event. Fails with 409 if strokeId already exists.");

        group.MapDelete("/live-sessions/{sessionId}/whiteboard/strokes/{strokeId}", HandleRemoveStroke)
            .RequireAuthenticatedUser()
            .Produces<WhiteboardStateDto>()
            .Produces(401)
            .Produces(404)
            .WithTags("Whiteboard")
            .WithSummary("Remove a freehand stroke")
            .WithDescription("Removes the stroke with the given ID. Broadcasts stroke-removed SSE event. Fails with 404 if strokeId not found.");

        group.MapPut("/live-sessions/{sessionId}/whiteboard/structured", HandleUpdateStructured)
            .RequireAuthenticatedUser()
            .Produces<WhiteboardStateDto>()
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .WithTags("Whiteboard")
            .WithSummary("Update structured layer")
            .WithDescription("Replaces the structured layer (token positions, grid config). Max 100 KB. Broadcasts structured-updated SSE event.");

        group.MapDelete("/live-sessions/{sessionId}/whiteboard", HandleClear)
            .RequireAuthenticatedUser()
            .Produces<WhiteboardStateDto>()
            .Produces(401)
            .Produces(404)
            .WithTags("Whiteboard")
            .WithSummary("Clear the whiteboard")
            .WithDescription("Removes all strokes and resets the structured layer to empty. Broadcasts whiteboard-cleared SSE event.");

        // === Queries ===

        group.MapGet("/live-sessions/{sessionId}/whiteboard", HandleGet)
            .RequireAuthenticatedUser()
            .Produces<WhiteboardStateDto>()
            .Produces(401)
            .Produces(404)
            .WithTags("Whiteboard")
            .WithSummary("Get whiteboard state")
            .WithDescription("Returns the current WhiteboardState including all strokes and the structured layer.");

        return group;
    }

    private static async Task<IResult> HandleInitialize(
        Guid sessionId, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(context, out var userId))
            return Results.Unauthorized();

        var command = new InitializeWhiteboardCommand(sessionId, userId);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}/whiteboard", result);
    }

    private static async Task<IResult> HandleAddStroke(
        Guid sessionId, AddStrokeRequest request, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(context, out var userId))
            return Results.Unauthorized();

        var command = new AddStrokeCommand(sessionId, request.StrokeId, request.DataJson, userId);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRemoveStroke(
        Guid sessionId, string strokeId, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(context, out var userId))
            return Results.Unauthorized();

        var command = new RemoveStrokeCommand(sessionId, strokeId, userId);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateStructured(
        Guid sessionId, UpdateStructuredRequest request, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(context, out var userId))
            return Results.Unauthorized();

        var command = new UpdateStructuredCommand(sessionId, request.StructuredJson, userId);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleClear(
        Guid sessionId, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(context, out var userId))
            return Results.Unauthorized();

        var command = new ClearWhiteboardCommand(sessionId, userId);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGet(
        Guid sessionId, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWhiteboardStateQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static bool TryGetUserId(HttpContext context, out Guid userId)
    {
        userId = Guid.Empty;
        var claim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out userId);
    }
}

/// <summary>Request body for adding a freehand stroke.</summary>
internal sealed record AddStrokeRequest(string StrokeId, string DataJson);

/// <summary>Request body for updating the structured layer.</summary>
internal sealed record UpdateStructuredRequest(string StructuredJson);
