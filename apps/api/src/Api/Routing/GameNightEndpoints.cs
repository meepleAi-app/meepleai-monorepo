using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Game night event endpoints for creating, managing, and RSVPing to game nights.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal static class GameNightEndpoints
{
    public static RouteGroupBuilder MapGameNightEndpoints(this RouteGroupBuilder group)
    {
        var gameNights = group.MapGroup("/game-nights")
            .WithTags("GameNights");

        // Commands
        gameNights.MapPost("/", HandleCreateGameNight)
            .RequireAuthenticatedUser()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(401)
            .WithName("CreateGameNight")
            .WithSummary("Create a new game night event")
            .WithDescription("Creates a new game night event in Draft status.");

        gameNights.MapGet("/", HandleGetUpcomingGameNights)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<GameNightDto>>(200)
            .Produces(401)
            .WithName("GetUpcomingGameNights")
            .WithSummary("Get upcoming game nights")
            .WithDescription("Retrieves upcoming published game nights ordered by scheduled date.");

        gameNights.MapGet("/mine", HandleGetMyGameNights)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<GameNightDto>>(200)
            .Produces(401)
            .WithName("GetMyGameNights")
            .WithSummary("Get my game nights")
            .WithDescription("Retrieves game nights where the user is organizer or invited.");

        // Issue #950 (W1-PR2): regulars feed for Step 3 wizard suggestion list.
        gameNights.MapGet("/regulars", HandleGetRegulars)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<RegularDto>>(200)
            .Produces(401)
            .WithName("GetRegulars")
            .WithSummary("Get my regular co-participants")
            .WithDescription("Returns registered users invited to past game nights organized by the current user (12-month window), ranked by event count. Spec §7b.2.");

        // Issue #950 (W1-PR2): conflict-check feed for Step 1 wizard date warning.
        gameNights.MapGet("/check-conflict", HandleCheckConflict)
            .RequireAuthenticatedUser()
            .Produces<ConflictCheckDto>(200)
            .Produces(400)
            .Produces(401)
            .WithName("CheckGameNightConflict")
            .WithSummary("Check for game night scheduling conflicts")
            .WithDescription("Returns conflicts within ±2 hours of the proposed start time for the current user's calendar (as organizer or invitee). Spec §7b.3.");

        gameNights.MapGet("/{id:guid}", HandleGetGameNightById)
            .RequireAuthenticatedUser()
            .Produces<GameNightDto>(200)
            .Produces(404)
            .Produces(401)
            .WithName("GetGameNightById")
            .WithSummary("Get a game night by ID")
            .WithDescription("Retrieves full details of a game night event.");

        gameNights.MapPut("/{id:guid}", HandleUpdateGameNight)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .Produces(401)
            .WithName("UpdateGameNight")
            .WithSummary("Update a game night")
            .WithDescription("Updates a game night event. Only the organizer can update.");

        gameNights.MapPost("/{id:guid}/publish", HandlePublishGameNight)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithName("PublishGameNight")
            .WithSummary("Publish a game night")
            .WithDescription("Publishes a draft game night, making it visible and sending invitations.");

        gameNights.MapPost("/{id:guid}/cancel", HandleCancelGameNight)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithName("CancelGameNight")
            .WithSummary("Cancel a game night")
            .WithDescription("Cancels a game night event. Only the organizer can cancel.");

        gameNights.MapPost("/{id:guid}/invite", HandleInviteToGameNight)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithName("InviteToGameNight")
            .WithSummary("Invite users to a game night")
            .WithDescription("Invites additional users to a published game night.");

        gameNights.MapGet("/{id:guid}/rsvps", HandleGetGameNightRsvps)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<GameNightRsvpDto>>(200)
            .Produces(404)
            .Produces(401)
            .WithName("GetGameNightRsvps")
            .WithSummary("Get game night RSVPs")
            .WithDescription("Retrieves all RSVPs for a game night.");

        gameNights.MapPost("/{id:guid}/rsvp", HandleRespondToGameNight)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .Produces(401)
            .WithName("RespondToGameNight")
            .WithSummary("Respond to a game night invitation")
            .WithDescription("Submits an RSVP response (Accepted, Declined, Maybe) to a game night invitation.");

        // ── Public Token-Based RSVP Endpoints (Issue #607) ──────────────────

        gameNights.MapGet("/invitations/{token}", HandleGetGameNightInvitationByToken)
            .AllowAnonymous()
            .RequireRateLimiting("GameNightTokenRead")
            .Produces<PublicGameNightInvitationDto>(200)
            .Produces(404)
            .Produces(410)
            .Produces(429)
            .WithName("GetGameNightInvitationByToken")
            .WithSummary("Get a game night invitation by token")
            .WithDescription("Public endpoint to retrieve a game night invitation by its opaque token. Returns 410 Gone for terminal states (Expired, Cancelled). Rate-limited at 60 req/min per IP (issue #1169).");

        gameNights.MapPost("/invitations/{token}/respond", HandleRespondToGameNightInvitationByToken)
            .AllowAnonymous()
            .RequireRateLimiting("GameNightTokenRespond")
            .Produces<PublicGameNightInvitationDto>(200)
            .Produces(400)
            .Produces(404)
            .Produces(409)
            .Produces(410)
            .Produces(429)
            .WithName("RespondToGameNightInvitationByToken")
            .WithSummary("Respond to a game night invitation by token")
            .WithDescription("Public endpoint to submit an Accepted or Declined response, optionally with a guest display name (issue #1169). Optional auth: authenticated callers have their UserId attached. Idempotent on same-state; conflicts on switching across statuses. Rate-limited at 10 req/min per IP.");

        gameNights.MapPost("/{gameNightId:guid}/invitations", HandleCreateGameNightInvitationByEmail)
            .RequireAuthenticatedUser()
            .Produces<GameNightInvitationDto>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)
            .Produces(422)
            .WithName("CreateGameNightInvitationByEmail")
            .WithSummary("Create a token-addressable invitation by email")
            .WithDescription("Organizer-only endpoint to issue an opaque-token invitation to a non-platform email. Returns 409 if a pending invitation for the same email already exists.");

        // ── Game Night Experience v2 Endpoints ──────────────────────────────

        gameNights.MapPost("/{id:guid}/sessions", HandleStartGameNightSession)
            .RequireAuthenticatedUser()
            .Produces<StartGameNightSessionResult>(201)
            .Produces(400)
            .Produces(403)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithName("StartGameNightSession")
            .WithSummary("Start next game in the night")
            .WithDescription("Creates and starts a new game session within the game night. Only the organizer can start sessions.");

        gameNights.MapPost("/{id:guid}/sessions/complete", HandleCompleteGameNightSession)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(403)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithName("CompleteGameNightSession")
            .WithSummary("Complete current game in the night")
            .WithDescription("Completes the currently in-progress game session, optionally recording a winner.");

        gameNights.MapPost("/{id:guid}/finalize", HandleFinalizeGameNight)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(403)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithName("FinalizeGameNight")
            .WithSummary("Finalize the game night")
            .WithDescription("Ends the entire game night, transitioning to Completed status. All sessions must be finished.");

        gameNights.MapGet("/{id:guid}/diary", HandleGetGameNightDiary)
            .RequireAuthenticatedUser()
            .Produces<GameNightDiaryDto>(200)
            .Produces(404)
            .Produces(401)
            .WithName("GetGameNightDiary")
            .WithSummary("Get game night diary timeline")
            .WithDescription("Retrieves the cross-game diary timeline with all session events for the game night.");

        return group;
    }

    #region Command Handlers

    private static async Task<IResult> HandleCreateGameNight(
        [FromBody] CreateGameNightRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new CreateGameNightCommand(
            UserId: userId,
            Title: request.Title,
            ScheduledAt: request.ScheduledAt,
            Description: request.Description,
            Location: request.Location,
            MaxPlayers: request.MaxPlayers,
            GameIds: request.GameIds,
            InvitedUserIds: request.InvitedUserIds,
            InvitedEmails: request.InvitedEmails);

        var id = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/game-nights/{id}", id);
    }

    private static async Task<IResult> HandleUpdateGameNight(
        Guid id,
        [FromBody] UpdateGameNightRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new UpdateGameNightCommand(
            GameNightId: id,
            UserId: userId,
            Title: request.Title,
            ScheduledAt: request.ScheduledAt,
            Description: request.Description,
            Location: request.Location,
            MaxPlayers: request.MaxPlayers,
            GameIds: request.GameIds);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandlePublishGameNight(
        Guid id,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new PublishGameNightCommand(id, userId);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleCancelGameNight(
        Guid id,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new CancelGameNightCommand(id, userId);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleInviteToGameNight(
        Guid id,
        [FromBody] InviteToGameNightRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new InviteToGameNightCommand(id, userId, request.UserIds);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleRespondToGameNight(
        Guid id,
        [FromBody] RespondToGameNightRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        if (!Enum.TryParse<RsvpStatus>(request.Response, true, out var rsvpStatus) || rsvpStatus == RsvpStatus.Pending)
            return Results.BadRequest(new { error = "Invalid RSVP response. Must be Accepted, Declined, or Maybe." });

        var command = new RespondToGameNightCommand(id, userId, rsvpStatus);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleCreateGameNightInvitationByEmail(
        Guid gameNightId,
        [FromBody] CreateInvitationByEmailRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new CreateGameNightInvitationByEmailCommand(
            GameNightId: gameNightId,
            Email: request.Email,
            OrganizerUserId: userId);

        var dto = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/game-nights/{gameNightId}/invitations/{dto.Id}", dto);
    }

    private static async Task<IResult> HandleRespondToGameNightInvitationByToken(
        string token,
        [FromBody] RespondToInvitationByTokenRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<GameNightInvitationStatus>(request.Response, ignoreCase: true, out var status)
            || (status != GameNightInvitationStatus.Accepted && status != GameNightInvitationStatus.Declined))
        {
            return Results.BadRequest(new { error = "Invalid response. Must be Accepted or Declined." });
        }

        // Issue #1169: optional guest display name. Trim here so the persisted
        // form matches what the UI will echo back via PublicGameNightInvitationDto.RespondedByName.
        // Length is enforced at the endpoint because RespondToGameNightInvitationByTokenCommand
        // is `ICommand` (no TResponse) and MediatR's pipeline behaviors are
        // registered against IRequest<TResponse> — the FluentValidation
        // ValidationBehavior therefore does NOT run for void commands here.
        // The aggregate's NormalizeRespondedByName is defense-in-depth only.
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? null
            : request.DisplayName.Trim();

        if (displayName is not null
            && displayName.Length > Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent
                .GameNightInvitation.MaxRespondedByNameLength)
        {
            return Results.BadRequest(new
            {
                error = $"Display name must be "
                    + $"{Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent.GameNightInvitation.MaxRespondedByNameLength}"
                    + " characters or fewer."
            });
        }

        var rawUserId = httpContext.User.GetUserId();
        var responderUserId = rawUserId == Guid.Empty ? (Guid?)null : rawUserId;

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token: token,
            Response: status,
            ResponderUserId: responderUserId,
            ResponderDisplayName: displayName);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        var updated = await mediator.Send(
            new GetGameNightInvitationByTokenQuery(token), cancellationToken).ConfigureAwait(false);

        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }

    private static async Task<IResult> HandleStartGameNightSession(
        Guid id,
        [FromBody] StartGameNightSessionRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new StartGameNightSessionCommand(
            GameNightId: id,
            GameId: request.GameId,
            GameTitle: request.GameTitle,
            UserId: userId);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/game-nights/{id}/sessions/{result.GameNightSessionId}", result);
    }

    private static async Task<IResult> HandleCompleteGameNightSession(
        Guid id,
        [FromBody] CompleteGameNightSessionRequest? request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new CompleteGameNightSessionCommand(
            GameNightId: id,
            WinnerId: request?.WinnerId,
            UserId: userId);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleFinalizeGameNight(
        Guid id,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var command = new FinalizeGameNightCommand(id, userId);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    #endregion

    #region Query Handlers

    private static async Task<IResult> HandleGetGameNightDiary(
        Guid id,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGameNightDiaryQuery(id), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUpcomingGameNights(
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUpcomingGameNightsQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetMyGameNights(
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var result = await mediator.Send(new GetMyGameNightsQuery(userId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // Issue #950 (W1-PR2): regulars feed for Step 3 wizard suggestion list. Spec §7b.2.
    private static async Task<IResult> HandleGetRegulars(
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        [FromQuery] int? limit = null)
    {
        var userId = httpContext.User.GetUserId();

        // Spec §7b.2: default limit 10, max 30. Clamp server-side to prevent
        // unbounded queries via query-string manipulation.
        var clamped = Math.Clamp(limit ?? 10, 1, 30);

        var result = await mediator.Send(new GetRegularsQuery(userId, clamped), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // Issue #950 (W1-PR2): conflict-check feed for Step 1 wizard date warning. Spec §7b.3.
    private static async Task<IResult> HandleCheckConflict(
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        [FromQuery] DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        if (!at.HasValue)
        {
            return Results.BadRequest(new { error = "Query parameter 'at' is required (ISO 8601 datetime offset)." });
        }

        // Spec §7b.3: reject horizons further than 2 years out — defensive bound
        // against accidental Unix-epoch-style overflows from clients.
        if (at.Value > DateTimeOffset.UtcNow.AddYears(2))
        {
            return Results.BadRequest(new { error = "Query parameter 'at' cannot be more than 2 years in the future." });
        }

        var userId = httpContext.User.GetUserId();
        var result = await mediator.Send(new CheckGameNightConflictQuery(userId, at.Value), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameNightById(
        Guid id,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGameNightByIdQuery(id), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameNightRsvps(
        Guid id,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGameNightRsvpsQuery(id), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameNightInvitationByToken(
        string token,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetGameNightInvitationByTokenQuery(token), cancellationToken).ConfigureAwait(false);

        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    #endregion

    #region Request Records

    private sealed record CreateGameNightRequest(
        string Title,
        DateTimeOffset ScheduledAt,
        string? Description = null,
        string? Location = null,
        int? MaxPlayers = null,
        List<Guid>? GameIds = null,
        List<Guid>? InvitedUserIds = null,
        List<string>? InvitedEmails = null);

    private sealed record UpdateGameNightRequest(
        string Title,
        DateTimeOffset ScheduledAt,
        string? Description = null,
        string? Location = null,
        int? MaxPlayers = null,
        List<Guid>? GameIds = null);

    private sealed record InviteToGameNightRequest(List<Guid> UserIds);

    private sealed record RespondToGameNightRequest(string Response);

    // Public token-based RSVP request records (Issue #607)
    private sealed record CreateInvitationByEmailRequest(string Email);

    /// <summary>
    /// Public RSVP-by-token request body. <c>DisplayName</c> is optional
    /// (issue #1169): when omitted the response is recorded anonymously;
    /// when present it is trimmed and capped at 120 characters by the
    /// validator before reaching the aggregate.
    /// </summary>
    private sealed record RespondToInvitationByTokenRequest(
        string Response,
        string? DisplayName = null);

    // Game Night Experience v2 request records
    private sealed record StartGameNightSessionRequest(Guid GameId, string GameTitle);
    private sealed record CompleteGameNightSessionRequest(Guid? WinnerId);

    #endregion
}
