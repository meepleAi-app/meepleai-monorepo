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
            InvitedUserIds: request.InvitedUserIds);

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

    #endregion

    #region Request Records

    private sealed record CreateGameNightRequest(
        string Title,
        DateTimeOffset ScheduledAt,
        string? Description = null,
        string? Location = null,
        int? MaxPlayers = null,
        List<Guid>? GameIds = null,
        List<Guid>? InvitedUserIds = null);

    private sealed record UpdateGameNightRequest(
        string Title,
        DateTimeOffset ScheduledAt,
        string? Description = null,
        string? Location = null,
        int? MaxPlayers = null,
        List<Guid>? GameIds = null);

    private sealed record InviteToGameNightRequest(List<Guid> UserIds);

    private sealed record RespondToGameNightRequest(string Response);

    // Game Night Experience v2 request records
    private sealed record StartGameNightSessionRequest(Guid GameId, string GameTitle);
    private sealed record CompleteGameNightSessionRequest(Guid? WinnerId);

    #endregion
}
