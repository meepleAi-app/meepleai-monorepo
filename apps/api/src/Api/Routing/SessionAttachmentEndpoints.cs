using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// REST API endpoints for session photo attachments.
/// Issue #5365 - Session attachment API endpoints.
/// </summary>
internal static class SessionAttachmentEndpoints
{
    public static RouteGroupBuilder MapSessionAttachmentEndpoints(this RouteGroupBuilder group)
    {
        // === Upload ===
        group.MapPost("/live-sessions/{sessionId}/attachments", HandleUploadAttachment)
            .RequireAuthenticatedUser()
            .DisableAntiforgery()
            .Produces<SessionAttachmentDto>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)
            .WithTags("SessionAttachments")
            .WithSummary("Upload a session photo attachment")
            .WithDescription("Uploads a photo (JPEG/PNG, max 10MB) to a live session. Requires active participant. Max 5 per player per snapshot.");

        // === List ===
        group.MapGet("/live-sessions/{sessionId}/attachments", HandleGetAttachments)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<SessionAttachmentDto>>(200)
            .Produces(401)
            .WithTags("SessionAttachments")
            .WithSummary("List session attachments")
            .WithDescription("Lists attachments for a session with optional filters: playerId, snapshotIndex, type.");

        // === Get by ID ===
        group.MapGet("/live-sessions/{sessionId}/attachments/{attachmentId}", HandleGetAttachmentById)
            .RequireAuthenticatedUser()
            .Produces<SessionAttachmentDetailDto>(200)
            .Produces(401)
            .Produces(404)
            .WithTags("SessionAttachments")
            .WithSummary("Get attachment details with download URL")
            .WithDescription("Returns attachment details including a pre-signed download URL.");

        // === Snapshot photos ===
        group.MapGet("/live-sessions/{sessionId}/snapshots/{index:int}/photos", HandleGetSnapshotPhotos)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<SessionAttachmentDto>>(200)
            .Produces(401)
            .WithTags("SessionAttachments")
            .WithSummary("Get photos for a snapshot")
            .WithDescription("Returns all photo attachments linked to a specific snapshot index.");

        // === Delete ===
        group.MapDelete("/live-sessions/{sessionId}/attachments/{attachmentId}", HandleDeleteAttachment)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .WithTags("SessionAttachments")
            .WithSummary("Delete a session attachment")
            .WithDescription("Soft-deletes an attachment. Only the photo owner or session host can delete.");

        return group;
    }

    #region Handlers

    private static async Task<IResult> HandleUploadAttachment(
        Guid sessionId,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var file = form.Files.GetFile("file");

        if (file == null || file.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        var attachmentTypeStr = form["attachmentType"].ToString();
        if (!Enum.TryParse<AttachmentType>(attachmentTypeStr, ignoreCase: true, out var attachmentType))
            return Results.BadRequest(new { error = "Invalid or missing attachmentType." });

        var caption = form["caption"].ToString();
        int? snapshotIndex = null;
        var snapshotStr = form["snapshotIndex"].ToString();
        if (!string.IsNullOrEmpty(snapshotStr) && int.TryParse(snapshotStr, System.Globalization.CultureInfo.InvariantCulture, out var parsedSnapshot))
            snapshotIndex = parsedSnapshot;

        var playerIdStr = form["playerId"].ToString();
        if (!Guid.TryParse(playerIdStr, out var playerId))
            return Results.BadRequest(new { error = "Invalid or missing playerId." });

        var command = new UploadSessionAttachmentCommand(
            sessionId,
            playerId,
            file.OpenReadStream(),
            file.FileName,
            file.ContentType,
            file.Length,
            attachmentType,
            string.IsNullOrWhiteSpace(caption) ? null : caption,
            snapshotIndex);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}/attachments/{result.Id}", result);
    }

    private static async Task<IResult> HandleGetAttachments(
        Guid sessionId,
        [FromQuery] Guid? playerId,
        [FromQuery] int? snapshotIndex,
        [FromQuery] AttachmentType? type,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetSessionAttachmentsQuery(sessionId, playerId, snapshotIndex, type);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAttachmentById(
        Guid sessionId,
        Guid attachmentId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetSessionAttachmentByIdQuery(sessionId, attachmentId);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSnapshotPhotos(
        Guid sessionId,
        int index,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetSnapshotPhotosQuery(sessionId, index);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteAttachment(
        Guid sessionId,
        Guid attachmentId,
        [FromQuery] Guid playerId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteSessionAttachmentCommand(sessionId, attachmentId, playerId);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    #endregion
}
