using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Live game session endpoints for real-time session management.
/// Issue #4749: CQRS commands/queries + API endpoints for live sessions.
/// </summary>
internal static class LiveSessionEndpoints
{
    public static RouteGroupBuilder MapLiveSessionEndpoints(this RouteGroupBuilder group)
    {
        // === Commands ===

        group.MapPost("/live-sessions", HandleCreateSession)
            .RequireAuthenticatedUser()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(401)
            .WithTags("LiveSessions")
            .WithSummary("Create a live game session")
            .WithDescription("Creates a new live game session with optional game link and scoring config.");

        group.MapPost("/live-sessions/{sessionId}/start", HandleStartSession)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Start a live session")
            .WithDescription("Transitions session from Created/Setup to InProgress.");

        group.MapPost("/live-sessions/{sessionId}/pause", HandlePauseSession)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Pause a live session");

        group.MapPost("/live-sessions/{sessionId}/resume", HandleResumeSession)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Resume a paused session");

        group.MapPost("/live-sessions/{sessionId}/complete", HandleCompleteSession)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Complete a live session")
            .WithDescription("Completes the session and triggers PlayRecord generation.");

        group.MapPost("/live-sessions/{sessionId}/save", HandleSaveSession)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Save session state");

        group.MapPost("/live-sessions/{sessionId}/players", HandleAddPlayer)
            .RequireAuthenticatedUser()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Add a player to the session");

        group.MapDelete("/live-sessions/{sessionId}/players/{playerId}", HandleRemovePlayer)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Remove a player from the session");

        group.MapPut("/live-sessions/{sessionId}/turn-order", HandleUpdateTurnOrder)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Update player turn order");

        group.MapPost("/live-sessions/{sessionId}/teams", HandleCreateTeam)
            .RequireAuthenticatedUser()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Create a team in the session");

        group.MapPut("/live-sessions/{sessionId}/teams/{teamId}/players/{playerId}", HandleAssignPlayerToTeam)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Assign a player to a team");

        group.MapPost("/live-sessions/{sessionId}/scores", HandleRecordScore)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Record a score entry");

        group.MapPut("/live-sessions/{sessionId}/scores", HandleEditScore)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Edit an existing score entry");

        group.MapPost("/live-sessions/{sessionId}/advance-turn", HandleAdvanceTurn)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Advance to the next turn");

        group.MapPut("/live-sessions/{sessionId}/notes", HandleUpdateNotes)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Update session notes");

        // === Queries ===
        // NOTE: Literal-segment routes MUST be registered before parameterized {sessionId} route
        // to prevent ASP.NET Core from trying to parse "active"/"code" as a Guid.

        group.MapGet("/live-sessions/active", HandleGetActiveSessions)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<LiveSessionSummaryDto>>(200)
            .WithTags("LiveSessions")
            .WithSummary("Get user's active sessions")
            .WithDescription("Retrieves all active (non-completed) sessions for the authenticated user.");

        group.MapGet("/live-sessions/code/{code}", HandleGetSessionByCode)
            .RequireAuthenticatedUser()
            .Produces<LiveSessionDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get live session by join code");

        group.MapGet("/live-sessions/{sessionId}", HandleGetSession)
            .RequireAuthenticatedUser()
            .Produces<LiveSessionDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get live session by ID")
            .WithDescription("Retrieves full session details including players, scores, and teams.");

        group.MapGet("/live-sessions/{sessionId}/scores", HandleGetSessionScores)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<LiveSessionRoundScoreDto>>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get scores for a session");

        group.MapGet("/live-sessions/{sessionId}/players", HandleGetSessionPlayers)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<LiveSessionPlayerDto>>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get players in a session");

        return group;
    }

    #region Command Handlers

    private static async Task<IResult> HandleCreateSession(
        [FromBody] CreateSessionRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new CreateLiveSessionCommand(
            userId,
            request.GameName,
            request.GameId,
            request.Visibility,
            request.GroupId,
            request.ScoringDimensions,
            request.DimensionUnits,
            request.AgentMode);

        var sessionId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}", sessionId);
    }

    private static async Task<IResult> HandleStartSession(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new StartLiveSessionCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandlePauseSession(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new PauseLiveSessionCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleResumeSession(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ResumeLiveSessionCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleCompleteSession(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteLiveSessionCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleSaveSession(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new SaveLiveSessionCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleAddPlayer(
        Guid sessionId,
        [FromBody] AddPlayerRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AddPlayerToLiveSessionCommand(
            sessionId,
            request.DisplayName,
            request.Color,
            request.UserId,
            request.Role,
            request.AvatarUrl);

        var playerId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}/players/{playerId}", playerId);
    }

    private static async Task<IResult> HandleRemovePlayer(
        Guid sessionId,
        Guid playerId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RemovePlayerFromLiveSessionCommand(sessionId, playerId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateTurnOrder(
        Guid sessionId,
        [FromBody] UpdateTurnOrderRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePlayerOrderCommand(sessionId, request.PlayerIds), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleCreateTeam(
        Guid sessionId,
        [FromBody] CreateTeamRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateLiveSessionTeamCommand(sessionId, request.Name, request.Color);
        var teamId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}/teams/{teamId}", teamId);
    }

    private static async Task<IResult> HandleAssignPlayerToTeam(
        Guid sessionId,
        Guid teamId,
        Guid playerId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AssignPlayerToTeamCommand(sessionId, playerId, teamId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleRecordScore(
        Guid sessionId,
        [FromBody] RecordScoreRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new RecordLiveSessionScoreCommand(
            sessionId, request.PlayerId, request.Round, request.Dimension, request.Value, request.Unit);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleEditScore(
        Guid sessionId,
        [FromBody] RecordScoreRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new EditLiveSessionScoreCommand(
            sessionId, request.PlayerId, request.Round, request.Dimension, request.Value, request.Unit);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleAdvanceTurn(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AdvanceLiveSessionTurnCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateNotes(
        Guid sessionId,
        [FromBody] UpdateNotesRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateLiveSessionNotesCommand(sessionId, request.Notes), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    #endregion

    #region Query Handlers

    private static async Task<IResult> HandleGetSession(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLiveSessionQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionByCode(
        string code,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLiveSessionByCodeQuery(code), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetActiveSessions(
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var result = await mediator.Send(new GetUserActiveSessionsQuery(userId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionScores(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSessionScoresQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionPlayers(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSessionPlayersQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    #endregion

    #region Request Models

    private sealed record CreateSessionRequest(
        string GameName,
        Guid? GameId = null,
        PlayRecordVisibility Visibility = PlayRecordVisibility.Private,
        Guid? GroupId = null,
        List<string>? ScoringDimensions = null,
        Dictionary<string, string>? DimensionUnits = null,
        AgentSessionMode AgentMode = AgentSessionMode.None);

    private sealed record AddPlayerRequest(
        string DisplayName,
        PlayerColor Color,
        Guid? UserId = null,
        PlayerRole? Role = null,
        string? AvatarUrl = null);

    private sealed record UpdateTurnOrderRequest(List<Guid> PlayerIds);

    private sealed record CreateTeamRequest(string Name, string Color);

    private sealed record RecordScoreRequest(
        Guid PlayerId,
        int Round,
        string Dimension,
        int Value,
        string? Unit = null);

    private sealed record UpdateNotesRequest(string? Notes);

    #endregion
}
