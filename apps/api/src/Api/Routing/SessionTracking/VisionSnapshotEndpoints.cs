using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Vision snapshot endpoints for board photo capture and AI game state extraction.
/// Session Vision AI feature.
/// </summary>
internal static class VisionSnapshotEndpoints
{
    public static RouteGroupBuilder MapVisionSnapshotEndpoints(this RouteGroupBuilder group)
    {
        var snapshotGroup = group.MapGroup("/live-sessions/{sessionId:guid}/vision-snapshots")
            .WithTags("SessionTracking", "Vision");

        // POST / — create snapshot (multipart form)
        snapshotGroup.MapPost("", HandleCreateSnapshot)
            .RequireAuthenticatedUser()
            .DisableAntiforgery()
            .Produces<CreateVisionSnapshotResult>(201)
            .Produces(400)
            .Produces(404)
            .Produces(409)
            .WithSummary("Upload board photo(s) to create a vision snapshot")
            .WithDescription(
                "Creates a vision snapshot with uploaded images. " +
                "Accepts multipart/form-data with files and optional caption/turnNumber fields.");

        // GET / — list snapshots
        snapshotGroup.MapGet("", HandleGetSnapshots)
            .RequireAuthenticatedUser()
            .Produces<List<VisionSnapshotDto>>()
            .WithSummary("List all vision snapshots for a session")
            .WithDescription("Returns snapshots with presigned image download URLs.");

        // GET /game-state — latest game state
        snapshotGroup.MapGet("/game-state", HandleGetLatestGameState)
            .RequireAuthenticatedUser()
            .Produces<GameStateResult>()
            .Produces(204)
            .WithSummary("Get the latest extracted game state")
            .WithDescription(
                "Returns the most recent game state extracted from vision snapshots. " +
                "Returns 204 if no snapshot or no extraction exists.");

        return group;
    }

    private static async Task<IResult> HandleCreateSnapshot(
        Guid sessionId,
        HttpContext context,
        IMediator mediator,
        [FromForm] string? caption = null,
        [FromForm] int turnNumber = 0)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Read uploaded files from multipart form
        var form = await context.Request.ReadFormAsync().ConfigureAwait(false);
        var images = new List<VisionSnapshotImageUpload>();

        foreach (var file in form.Files)
        {
            if (file.Length == 0) continue;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms).ConfigureAwait(false);

            images.Add(new VisionSnapshotImageUpload(
                ms.ToArray(),
                file.ContentType,
                file.FileName));
        }

        var command = new CreateVisionSnapshotCommand(
            sessionId,
            userId,
            turnNumber,
            caption,
            images);

        var result = await mediator.Send(command).ConfigureAwait(false);
        return Results.Created(
            $"/api/v1/live-sessions/{sessionId}/vision-snapshots",
            result);
    }

    private static async Task<IResult> HandleGetSnapshots(
        Guid sessionId,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetVisionSnapshotsQuery(sessionId)).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetLatestGameState(
        Guid sessionId,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetLatestGameStateQuery(sessionId)).ConfigureAwait(false);
        return result is null ? Results.NoContent() : Results.Ok(result);
    }
}
