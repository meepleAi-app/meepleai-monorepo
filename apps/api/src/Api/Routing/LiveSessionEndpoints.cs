using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.Observability;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.BoundedContexts.GameManagement.Application.Queries.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Extensions;
using Api.SharedKernel.Domain.ValueObjects;
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
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)  // QuotaExceededException (DomainException → 400 via middleware) or zero active players
            .Produces(403)  // Non-creator caller (#2608: only the creator may start; maps to ForbiddenException → 403)
            .Produces(404)
            .Produces(409)  // DbUpdateConcurrencyException (xmin race — concurrent start)
            .WithTags("LiveSessions")
            .WithSummary("Start a live session")
            .WithDescription("Transitions session from Created/Setup to InProgress. Only the session creator may start (403). Quota limit returns 400. Concurrent start race returns 409.");

        group.MapPost("/live-sessions/{sessionId}/pause", HandlePauseSession)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Pause a live session");

        group.MapPost("/live-sessions/{sessionId}/resume", HandleResumeSession)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Resume a paused session");

        group.MapPost("/live-sessions/{sessionId}/complete", HandleCompleteSession)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Complete a live session")
            .WithDescription("Completes the session and triggers PlayRecord generation.");

        group.MapPost("/live-sessions/{sessionId}/save", HandleSaveSession)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Save session state");

        group.MapPost("/live-sessions/{sessionId}/players", HandleAddPlayer)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Add a player to the session");

        group.MapDelete("/live-sessions/{sessionId}/players/{playerId}", HandleRemovePlayer)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Remove a player from the session");

        group.MapPut("/live-sessions/{sessionId}/turn-order", HandleUpdateTurnOrder)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Update player turn order");

        group.MapPost("/live-sessions/{sessionId}/teams", HandleCreateTeam)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Create a team in the session");

        group.MapPut("/live-sessions/{sessionId}/teams/{teamId}/players/{playerId}", HandleAssignPlayerToTeam)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Assign a player to a team");

        group.MapPost("/live-sessions/{sessionId}/scores", HandleRecordScore)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Record a score entry");

        group.MapPut("/live-sessions/{sessionId}/scores", HandleEditScore)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Edit an existing score entry");

        group.MapPost("/live-sessions/{sessionId}/advance-turn", HandleAdvanceTurn)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Advance to the next turn");

        group.MapPost("/live-sessions/{sessionId}/advance-phase", HandleAdvancePhase)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Advance to the next turn phase")
            .WithDescription("Advances the current turn phase. If at the last phase, wraps to phase 0. Issue #4761.");

        group.MapPut("/live-sessions/{sessionId}/phases", HandleConfigurePhases)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Configure turn phases")
            .WithDescription("Sets the phase names for the session's turn structure. Issue #4761.");

        group.MapPost("/live-sessions/{sessionId}/trigger-snapshot", HandleTriggerSnapshot)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<SessionSnapshotDto>(201)
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Trigger an event-based snapshot")
            .WithDescription("Triggers a snapshot with debounce protection. Returns 204 if debounced. Issue #4761.");

        group.MapPut("/live-sessions/{sessionId}/notes", HandleUpdateNotes)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Update session notes");

        group.MapPut("/live-sessions/{sessionId}/game-state", HandleUpdateGameState)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(403)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Update the live game-state (#3025 L1)");

        group.MapPost("/live-sessions/{sessionId}/scores/parse", HandleParseScore)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<ScoreParseResultDto>(200)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Parse a natural language message for score data")
            .WithDescription("Parses a message and optionally auto-records the score if confidence is high enough.");

        group.MapPost("/live-sessions/{sessionId}/scores/confirm", HandleConfirmScore)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Confirm and record a previously parsed score");

        group.MapPost("/live-sessions/{sessionId}/save-complete", HandleSaveComplete)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<SessionSaveResultDto>(200)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Save complete session state")
            .WithDescription("Save complete session state with pause + snapshot + agent persist. Issue #122.");

        group.MapPost("/live-sessions/{sessionId}/setup-checklist", HandleGenerateSetupChecklist)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<SetupChecklistData>(200)
            .Produces(400)
            .Produces(404)
            .Produces(401)
            .WithTags("LiveSessions")
            .WithSummary("Generate setup checklist from game rulebook via RAG")
            .WithDescription("Generates a player-count-specific setup checklist by querying the game's PDF rulebook through the KnowledgeBase RAG pipeline.");

        group.MapPut("/live-sessions/{sessionId}/setup-checklist", HandleUpdateSetupChecklist)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .Produces(401)
            .WithTags("LiveSessions")
            .WithSummary("Update setup checklist state")
            .WithDescription("Replaces the setup checklist data, used when the user toggles components or completes steps in the setup wizard.");

        // === Diary (SP3) ===

        group.MapPost("/live-sessions/{sessionId}/diary", HandleAddDiaryEntry)
            .RequireAuthenticatedUser()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)
            .WithTags("LiveSessions")
            .WithSummary("Add a diary entry to a live session")
            .WithDescription("Appends an immutable diary entry to the session. Returns the new entry id. 403 if caller is not a participant. 409 if session is Completed. Issue #2570 SP3.");

        group.MapGet("/live-sessions/{sessionId}/diary", HandleGetDiary)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<DiaryEntryDto>>(200)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get diary entries for a live session")
            .WithDescription("Returns all diary entries ordered by CreatedAt ascending. 403 if caller is not a session participant. 404 if session not found. Issue #2570 SP3.");

        // === Dispute v2 ===

        group.MapPost("/live-sessions/{sessionId}/disputes", HandleOpenDispute)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Open a structured rule dispute")
            .WithDescription("Opens a new structured rule dispute during a live game session. Returns the dispute ID.");

        group.MapPut("/live-sessions/{sessionId}/disputes/{disputeId}/respond", HandleRespondToDispute)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Respond to a dispute")
            .WithDescription("Adds a respondent's counter-claim to an existing dispute.");

        group.MapPost("/live-sessions/{sessionId}/disputes/{disputeId}/timeout", HandleRespondentTimeout)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Trigger respondent timeout")
            .WithDescription("Handles the timeout scenario when a respondent does not reply to a dispute.");

        group.MapPost("/live-sessions/{sessionId}/disputes/{disputeId}/vote", HandleCastVote)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Cast a vote on a dispute verdict")
            .WithDescription("Records a player's vote on an active dispute verdict.");

        group.MapPost("/live-sessions/{sessionId}/disputes/{disputeId}/tally", HandleTallyVotes)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Tally dispute votes")
            .WithDescription("Tallies all cast votes on a dispute and determines the final outcome.");

        group.MapGet("/games/{gameId}/dispute-history", HandleGetDisputeHistory)
            .RequireAuthenticatedUser()
            .Produces<GetGameDisputeHistoryResult>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get game dispute history")
            .WithDescription("Returns all rule dispute entries across every session played for a given game.");

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

        group.MapGet("/live-sessions/code/{code}/public", HandleGetPublicSessionByCode)
            .AllowAnonymous()
            .RequireRateLimiting("LiveSessionCodeReadPublic")
            .Produces<PublicLiveSessionDto>(200)
            .Produces(404)
            .Produces(429)
            .WithTags("LiveSessions")
            .WithSummary("Get a live session lobby by join code (public, read-only)")
            .WithDescription("Public QR-code endpoint. Returns a narrow read-only lobby/scoreboard projection (no UserIds/Notes/scores history). Code-as-capability, optional auth. Rate-limited 60/min per IP. Issue #2590.");

        group.MapGet("/live-sessions/{sessionId}", HandleGetSession)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<LiveSessionDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get live session by ID")
            .WithDescription("Retrieves full session details including players, scores, and teams.");

        group.MapGet("/live-sessions/{sessionId}/scores", HandleGetSessionScores)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<IReadOnlyList<LiveSessionRoundScoreDto>>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get scores for a session");

        group.MapGet("/live-sessions/{sessionId}/players", HandleGetSessionPlayers)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<IReadOnlyList<LiveSessionPlayerDto>>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get players in a session");

        group.MapGet("/live-sessions/{sessionId}/phases", HandleGetTurnPhases)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<TurnPhasesDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get current turn phases")
            .WithDescription("Returns the current phase configuration and state. Issue #4761.");

        group.MapGet("/live-sessions/{sessionId}/tools", HandleGetSessionTools)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<SessionToolsDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get session toolkit")
            .WithDescription("Returns the four implicit base tools and any custom ToolState items for the session. Issue #4969.");

        group.MapGet("/live-sessions/{sessionId}/context", HandleGetSessionContext)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<GameSessionContextDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get game session context")
            .WithDescription("Builds a cross-context view of the session including expansions, KB cards, rulebook analyses, and degradation level. Issue #5579.");

        group.MapPost("/live-sessions/{sessionId}/context/refresh", HandleRefreshSessionContext)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<GameSessionContextDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Refresh game session context")
            .WithDescription("Rebuilds the session context, useful after indexing new PDFs or linking expansions. Issue #5579.");

        group.MapGet("/live-sessions/{sessionId}/resume-context", HandleGetResumeContext)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<SessionResumeContextDto>(200)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Get session resume context")
            .WithDescription("Get session resume context with recap, scores, and photos. Issue #122.");

        // NOTE: /{sessionId:guid}/stream uses a sub-segment and does NOT collide with /{sessionId:guid}
        // because ASP.NET Core route matching considers the trailing /stream literal.
        group.MapGet("/live-sessions/{sessionId:guid}/stream", HandleStreamAsync)
            .RequireAuthenticatedUser()
            .Produces(200)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Native SSE stream for live session events")
            .WithDescription(
                "Server-Sent Events stream for real-time live session updates via ILiveSessionStreamGateway. " +
                "Returns X-Warning-Code: stream-not-linked when session has no companion (TrackingSessionId null). " +
                "Issue #2561 SP2 T4.");

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
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Issue #2587 Slice 1: Resolve user identity for quota enforcement and GameSession correlation.
        var (authenticated, session, error) = httpContext.TryGetActiveSession();
        if (!authenticated) return error!;

        var userId = session!.Principal!.Subject.Id;
        var userTier = UserTier.Parse(session.Principal!.Subject.Tier);
        var userRole = Role.Parse(session.Principal!.EffectiveActor.Role);

        await mediator.Send(
            new StartLiveSessionCommand(sessionId, userId, userTier, userRole),
            cancellationToken).ConfigureAwait(false);
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

    private static async Task<IResult> HandleAdvancePhase(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AdvanceLiveSessionPhaseCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleConfigurePhases(
        Guid sessionId,
        [FromBody] ConfigurePhasesRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ConfigureLiveSessionPhasesCommand(sessionId, request.PhaseNames), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleTriggerSnapshot(
        Guid sessionId,
        [FromBody] TriggerSnapshotRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TriggerEventSnapshotCommand(
            sessionId, request.TriggerType, request.Description, request.CreatedByPlayerId),
            cancellationToken).ConfigureAwait(false);

        return result != null
            ? Results.Created($"/api/v1/live-sessions/{sessionId}/snapshots/{result.Id}", result)
            : Results.NoContent();
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

    private static async Task<IResult> HandleParseScore(
        Guid sessionId,
        [FromBody] ParseScoreRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ParseAndRecordScoreCommand
        {
            SessionId = sessionId,
            Message = request.Message,
            AutoRecord = request.AutoRecord
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleConfirmScore(
        Guid sessionId,
        [FromBody] ConfirmScoreRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmScoreCommand
        {
            SessionId = sessionId,
            PlayerId = request.PlayerId,
            Dimension = request.Dimension,
            Value = request.Value,
            Round = request.Round
        };

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleSaveComplete(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SaveCompleteSessionStateCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGenerateSetupChecklist(
        Guid sessionId,
        [FromBody] GenerateSetupChecklistRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var checklist = await mediator.Send(
            new GenerateSetupChecklistCommand(sessionId, request.PlayerCount), cancellationToken).ConfigureAwait(false);
        return Results.Ok(checklist);
    }

    private static async Task<IResult> HandleUpdateSetupChecklist(
        Guid sessionId,
        [FromBody] SetupChecklistData checklist,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new UpdateSetupChecklistCommand(sessionId, checklist), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    // === Diary handlers (SP3 T5) ===

    /// <summary>
    /// POST /api/v1/live-sessions/{sessionId}/diary — Issue #2570 SP3 T5.
    /// AuthorId is taken from the authenticated user's claims (not from the body).
    /// 409 (Completed session) and 404 (not found) propagate from the command handler via middleware.
    /// </summary>
    private static async Task<IResult> HandleAddDiaryEntry(
        Guid sessionId,
        [FromBody] AddDiaryEntryRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var authorId = httpContext.User.GetUserId();

        var entryId = await mediator.Send(
            new AddDiaryEntryCommand(sessionId, authorId, request.Text),
            cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/live-sessions/{sessionId}/diary/{entryId}", entryId);
    }

    /// <summary>
    /// PUT /api/v1/live-sessions/{sessionId}/game-state — #3025 L1.
    /// Replaces the opaque live game-state. 204 on success; 403 non-participant;
    /// 404 session not found; 409 if the session is Completed.
    /// </summary>
    private static async Task<IResult> HandleUpdateGameState(
        Guid sessionId,
        [FromBody] UpdateGameStateRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        await mediator.Send(
            new UpdateLiveGameStateCommand(sessionId, userId, request.State),
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    /// <summary>
    /// GET /api/v1/live-sessions/{sessionId}/diary — Issue #2570 SP3 T5.
    /// Returns all diary entries ordered by CreatedAt ascending. 404 if session not found.
    /// 403 if the caller is not the session creator or an active participant.
    /// </summary>
    private static async Task<IResult> HandleGetDiary(
        Guid sessionId,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var entries = await mediator.Send(
            new GetLiveSessionDiaryQuery(sessionId, userId),
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(entries);
    }

    private static async Task<IResult> HandleOpenDispute(
        Guid sessionId,
        [FromBody] OpenDisputeRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new OpenStructuredDisputeCommand(
            sessionId,
            request.InitiatorPlayerId,
            request.InitiatorClaim);

        var disputeId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}/disputes/{disputeId}", disputeId);
    }

    private static async Task<IResult> HandleRespondToDispute(
        Guid sessionId,
        Guid disputeId,
        [FromBody] RespondToDisputeRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new RespondToDisputeCommand(sessionId, disputeId, request.RespondentPlayerId, request.RespondentClaim),
            cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleRespondentTimeout(
        Guid sessionId,
        Guid disputeId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RespondentTimeoutCommand(sessionId, disputeId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleCastVote(
        Guid sessionId,
        Guid disputeId,
        [FromBody] CastVoteRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new CastVoteOnDisputeCommand(sessionId, disputeId, request.PlayerId, request.AcceptsVerdict),
            cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleTallyVotes(
        Guid sessionId,
        Guid disputeId,
        [FromBody] TallyVotesRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new TallyDisputeVotesCommand(sessionId, disputeId, request.OverrideRule),
            cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleGetDisputeHistory(
        Guid gameId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetGameDisputeHistoryQuery(gameId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
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

    /// <summary>
    /// GET /api/v1/live-sessions/code/{code}/public — Issue #2590.
    /// Public, anonymous, rate-limited narrow lobby projection. Returns 404 for an unknown code.
    /// </summary>
    private static async Task<IResult> HandleGetPublicSessionByCode(
        string code,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator
            .Send(new GetPublicLiveSessionByCodeQuery(code), cancellationToken)
            .ConfigureAwait(false);

        return result is null ? Results.NotFound() : Results.Ok(result);
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

    private static async Task<IResult> HandleGetTurnPhases(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTurnPhasesQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionTools(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSessionToolsQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionContext(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGameSessionContextQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRefreshSessionContext(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RefreshGameSessionContextQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetResumeContext(
        Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetSessionResumeContextQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// Native SSE stream for live session events — Issue #2561 SP2 T4.
    /// Mirrors the v2 legacy loop in SessionQueryEndpoints.cs:319-418 but uses
    /// ILiveSessionStreamGateway (GameManagement ACL) instead of ISessionBroadcastService.
    /// </summary>
    private static async Task<IResult> HandleStreamAsync(
        Guid sessionId,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromServices] ILiveSessionStreamGateway gateway,
        [FromQuery] string? lastEventId,
        CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();
        if (userId == Guid.Empty)
            return Results.Unauthorized();

        // CQRS authz + companion-presence check — no direct repo injection in endpoints.
        var ctx = await mediator
            .Send(new GetLiveSessionStreamContextQuery(sessionId, userId), ct)
            .ConfigureAwait(false);

        if (!ctx.Found)
            return Results.NotFound(new { error = "Live session not found" });

        if (!ctx.Authorized)
            return Results.StatusCode(403);

        // SP5-c (#2600 Task 2): lazily provision a companion for legacy GameId-backed sessions
        // that pre-date the SP0 Saga (TrackingSessionId == null && GameId != null).
        // The handler is idempotent and is a no-op for free-form (GameId == null) sessions and
        // for sessions that already have a companion. The gateway re-reads the session inside
        // SubscribeAsync so it will pick up the freshly-persisted TrackingSessionId.
        //
        // EnsureCompanionCommand returns the post-ensure TrackingSessionId:
        //   non-null → session is now linked (pre-existing OR just created on this subscribe)
        //   null     → free-form session; genuinely unlinked; stream will be empty.
        // We use this POST-ensure value for the warning header to avoid emitting a stale
        // "stream-not-linked" on the exact subscribe that just fixed the session (final-review fix).
        bool isLinkedAfterEnsure;
        if (ctx.HasCompanion)
        {
            // Already linked before we ran — skip the command, preserve the known state.
            isLinkedAfterEnsure = true;
        }
        else
        {
            var ensuredTrackingId = await mediator
                .Send(new EnsureCompanionCommand(sessionId), ct)
                .ConfigureAwait(false);
            isLinkedAfterEnsure = ensuredTrackingId.HasValue;
        }

        // Set SSE response headers — must happen before the first Write call.
        httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        // Warn caller when session has no companion: stream will be empty (gateway yields nothing).
        // The connection stays open with heartbeats so the client can reconnect after SP0 is provisioned.
        // Uses the POST-ensure value so a lazy-created companion on this subscribe does NOT trigger the warning.
        if (!isLinkedAfterEnsure)
            httpContext.Response.Headers.Append("X-Warning-Code", "stream-not-linked");

        // Commit the 200 status + SSE headers immediately, before any blocking gateway call.
        // Without this flush, TestHost and some proxies hold off sending the response until
        // the first body write — meaning an unhandled exception could still produce a 500 here.
        httpContext.Response.StatusCode = 200;
        await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);

        // AC-OBS-1 (#2561 SP2 T12): track active connections + reconnects.
        // Inc happens after auth + header flush so only live connections count.
        MeepleAiMetrics.IncrementLiveSseActiveConnections();
        if (!string.IsNullOrEmpty(lastEventId))
            MeepleAiMetrics.RecordLiveSseReconnect();

        // Heartbeat task: write a comment every 30 s to keep the connection alive.
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeatTask = Task.Run(async () =>
        {
            while (!heartbeatCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), heartbeatCts.Token).ConfigureAwait(false);
                    await httpContext.Response.WriteAsync(
                        $"event: heartbeat\ndata: {{\"timestamp\":\"{DateTime.UtcNow:O}\"}}\n\n",
                        heartbeatCts.Token).ConfigureAwait(false);
                    await httpContext.Response.Body.FlushAsync(heartbeatCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, heartbeatCts.Token);

        try
        {
            // gateway.SubscribeAsync returns an empty stream when HasCompanion == false.
            await foreach (var evt in gateway
                .SubscribeAsync(sessionId, userId, lastEventId, ct)
                .ConfigureAwait(false)
                .WithCancellation(ct))
            {
                var json = JsonSerializer.Serialize(evt.Data, SseJsonOptions);

                if (evt.Id is not null)
                    await httpContext.Response.WriteAsync($"id: {evt.Id}\n", ct).ConfigureAwait(false);

                await httpContext.Response.WriteAsync($"event: {evt.Type}\n", ct).ConfigureAwait(false);
                await httpContext.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected: client disconnected or server shutting down — do not propagate to middleware.
        }
        catch (Exception)
        {
            // I/O write fault on an already-committed 200 SSE stream (e.g. IOException,
            // ConnectionResetException) — swallow to avoid a spurious 500 in middleware logs.
            // The connection is already broken; nothing meaningful can be written.
        }
        finally
        {
            // AC-OBS-1 (#2561 SP2 T12): decrement active-connections gauge on every exit path.
            MeepleAiMetrics.DecrementLiveSseActiveConnections();

            await heartbeatCts.CancelAsync().ConfigureAwait(false);
            // Wait for heartbeat task to finish (it's already watching ct).
            // Both OperationCanceledException (normal) and any other I/O exception are benign here —
            // the heartbeat loop already exited because the connection is closing.
            try
            {
                await heartbeatTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: CancellationToken was cancelled — heartbeat task exited normally.
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                // I/O error after client disconnect — ignore, we're already in teardown.
            }
        }

        return Results.Empty;
    }

    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

    private sealed record ConfigurePhasesRequest(string[] PhaseNames);

    private sealed record TriggerSnapshotRequest(
        SnapshotTrigger TriggerType,
        string? Description = null,
        Guid? CreatedByPlayerId = null);

    private sealed record ParseScoreRequest(string Message, bool AutoRecord = true);

    private sealed record ConfirmScoreRequest(Guid PlayerId, string Dimension, int Value, int Round);

    private sealed record GenerateSetupChecklistRequest(int PlayerCount);

    // Diary (SP3) request model
    private sealed record AddDiaryEntryRequest(string Text);

    /// <summary>Body for PUT /live-sessions/{id}/game-state. Opaque JSON (#3025 L1).</summary>
    private sealed record UpdateGameStateRequest(System.Text.Json.JsonElement State);

    // Dispute v2 request models
    internal sealed record OpenDisputeRequest(
        Guid InitiatorPlayerId,
        string InitiatorClaim);

    internal sealed record RespondToDisputeRequest(
        Guid RespondentPlayerId,
        string RespondentClaim);

    internal sealed record CastVoteRequest(
        Guid PlayerId,
        bool AcceptsVerdict);

    internal sealed record TallyVotesRequest(
        string? OverrideRule = null);

    #endregion
}
