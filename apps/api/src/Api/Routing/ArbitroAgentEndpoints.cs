using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Arbitro agent endpoints for real-time move validation.
/// Issue #3759: Rules Arbitration Engine
/// </summary>
internal static class ArbitroAgentEndpoints
{
    public static RouteGroupBuilder MapArbitroAgentEndpoints(this RouteGroupBuilder group)
    {
        MapValidateMoveEndpoint(group);

        return group;
    }

    /// <summary>
    /// ISSUE-3759: Validate move using Arbitro agent.
    /// POST /agents/arbitro/validate
    /// </summary>
    private static void MapValidateMoveEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/arbitro/validate", async (
            ValidateMoveRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new ValidateMoveCommand(
                GameId: req.GameId,
                SessionId: req.SessionId,
                Move: req.Move,
                GameState: req.GameState
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Move validation completed for session {SessionId}: valid={IsValid}, confidence={Confidence}, time={Time}ms",
                req.SessionId, result.IsValid, result.Confidence, result.ExecutionTimeMs);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("ValidateMove")
        .WithTags("Agents", "Arbitro")
        .Produces<ValidateMoveResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(500);
    }
}

/// <summary>
/// Request for move validation.
/// </summary>
internal record ValidateMoveRequest(
    Guid GameId,
    Guid SessionId,
    string Move,
    string GameState
);
