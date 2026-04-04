using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Random tools endpoints (Issue #3345): timer (start/pause/resume/reset), coin flip, wheel spin.
/// </summary>
internal static class SessionToolsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapStartTimerEndpoint(group);
        MapPauseTimerEndpoint(group);
        MapResumeTimerEndpoint(group);
        MapResetTimerEndpoint(group);
        MapFlipCoinEndpoint(group);
        MapSpinWheelEndpoint(group);
    }

    private static void MapStartTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/start", async (
            Guid sessionId,
            StartTimerCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("StartTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Start a countdown timer for the session")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapPauseTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/pause", async (
            Guid sessionId,
            PauseTimerCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("PauseTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Pause the session timer")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapResumeTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/resume", async (
            Guid sessionId,
            ResumeTimerCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("ResumeTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Resume a paused session timer")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapResetTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/reset", async (
            Guid sessionId,
            ResetTimerCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("ResetTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Reset the session timer")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapFlipCoinEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/coin-flip", async (
            Guid sessionId,
            FlipCoinCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("FlipCoin")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Flip a coin for the session")
        .WithDescription("Returns a cryptographically secure random heads or tails result.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapSpinWheelEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/wheel-spin", async (
            Guid sessionId,
            SpinWheelCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SpinWheel")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Spin a wheel with custom options")
        .WithDescription("Performs weighted random selection from the provided options.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }
}
