using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Private notes endpoints (Issue #3344): save, reveal, hide, delete, get notes.
/// </summary>
internal static class SessionNotesEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapSavePrivateNoteEndpoint(group);
        MapRevealNoteEndpoint(group);
        MapHideNoteEndpoint(group);
        MapDeletePrivateNoteEndpoint(group);
        MapGetSessionNotesEndpoint(group);
        MapGetNoteByIdEndpoint(group);
    }

    private static void MapSavePrivateNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/private-notes", async (
            Guid sessionId,
            SaveNoteCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/private-notes/{result.NoteId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("SavePrivateNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Save or update a private note")
        .WithDescription("Creates a new encrypted private note or updates an existing one. Notes are private by default.")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(403);
    }

    private static void MapRevealNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}/reveal", async (
            Guid sessionId,
            Guid noteId,
            RevealNoteCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId || noteId != command.NoteId)
            {
                return Results.BadRequest(new { error = "Session or Note ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("RevealNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Reveal a private note to all participants")
        .WithDescription("Makes the note content visible to all session participants. Only the note owner can reveal.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapHideNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}/hide", async (
            Guid sessionId,
            Guid noteId,
            HideNoteCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId || noteId != command.NoteId)
            {
                return Results.BadRequest(new { error = "Session or Note ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("HideNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Hide a previously revealed note")
        .WithDescription("Makes the note private again. Only the note owner can hide.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapDeletePrivateNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}", async (
            Guid sessionId,
            Guid noteId,
            Guid participantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DeleteNoteCommand(noteId, sessionId, participantId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("DeletePrivateNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Delete a private note")
        .WithDescription("Soft-deletes a note. Only the note owner can delete.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapGetSessionNotesEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/private-notes", async (
            Guid sessionId,
            Guid requesterId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSessionNotesQuery(sessionId, requesterId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionNotes")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Get all visible notes in the session")
        .WithDescription("Returns the requester's own notes and any revealed notes from other participants.")
        .Produces(200)
        .Produces(401);
    }

    private static void MapGetNoteByIdEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}", async (
            Guid sessionId,
            Guid noteId,
            Guid requesterId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetNoteByIdQuery(noteId, requesterId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result is null)
            {
                return Results.NotFound(new { error = "Note not found or not accessible" });
            }

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetNoteById")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Get a specific note by ID")
        .WithDescription("Returns the note if the requester is the owner or if the note is revealed.")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }
}
