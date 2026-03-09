using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Queries.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Api.Routing;

/// <summary>
/// Session snapshot endpoints for time-travel and history management.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal static class SessionSnapshotEndpoints
{
    public static RouteGroupBuilder MapSessionSnapshotEndpoints(this RouteGroupBuilder group)
    {
        // === Queries ===

        group.MapGet("/sessions/{sessionId}/snapshots", HandleGetSnapshots)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<SessionSnapshotDto>>()
            .Produces(400)
            .WithTags("SessionSnapshots")
            .WithSummary("Get all snapshots for a session")
            .WithDescription("Returns the list of all snapshots (metadata only) for a live session.");

        group.MapGet("/sessions/{sessionId}/snapshots/{index:int}", HandleGetSnapshotState)
            .RequireAuthenticatedUser()
            .Produces<ReconstructedStateDto>()
            .Produces(404)
            .WithTags("SessionSnapshots")
            .WithSummary("Get reconstructed state at snapshot index")
            .WithDescription("Reconstructs the full session state at the specified snapshot index using checkpoint + deltas.");

        // === Commands ===

        group.MapPost("/sessions/{sessionId}/snapshots", HandleCreateManualSnapshot)
            .RequireAuthenticatedUser()
            .Produces<SessionSnapshotDto>(201)
            .Produces(400)
            .Produces(404)
            .WithTags("SessionSnapshots")
            .WithSummary("Create a manual snapshot")
            .WithDescription("Creates a manual save snapshot of the current session state.");

        group.MapPost("/sessions/{sessionId}/snapshots/{index:int}/restore", HandleRestoreSnapshot)
            .RequireAuthenticatedUser()
            .Produces<SessionSnapshotDto>(200)
            .Produces(400)
            .Produces(404)
            .Produces(409)
            .WithTags("SessionSnapshots")
            .WithSummary("Restore session state from a snapshot")
            .WithDescription("Restores the session to the state at the specified snapshot index. Auto-creates a pre-restore snapshot of the current state. Issue #5581.");

        return group;
    }

    private static async Task<IResult> HandleGetSnapshots(
        Guid sessionId, IMediator mediator)
    {
        var query = new GetSnapshotsQuery(sessionId);
        var result = await mediator.Send(query).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSnapshotState(
        Guid sessionId, int index, IMediator mediator)
    {
        var query = new GetSnapshotStateQuery(sessionId, index);
        var result = await mediator.Send(query).ConfigureAwait(false);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> HandleCreateManualSnapshot(
        Guid sessionId, CreateManualSnapshotRequest request,
        HttpContext context, IMediator mediator)
    {
        var playerId = context.User.GetUserId();
        var command = new CreateSnapshotCommand(
            sessionId,
            SnapshotTrigger.ManualSave,
            request.Description,
            playerId);

        var result = await mediator.Send(command).ConfigureAwait(false);
        return Results.Created(
            $"/api/v1/sessions/{sessionId}/snapshots/{result.SnapshotIndex}", result);
    }

    private static async Task<IResult> HandleRestoreSnapshot(
        Guid sessionId, int index, IMediator mediator)
    {
        var command = new RestoreSessionSnapshotCommand(sessionId, index);
        var result = await mediator.Send(command).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
