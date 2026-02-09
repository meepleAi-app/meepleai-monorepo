using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Arbitro agent endpoints for real-time move validation.
/// Issue #3760: Arbitro Agent Move Validation Logic with Game State Analysis.
/// </summary>
internal static class ArbitroAgentEndpoints
{
    public static RouteGroupBuilder MapArbitroAgentEndpoints(this RouteGroupBuilder group)
    {
        MapValidateMoveEndpoint(group);

        return group;
    }

    /// <summary>
    /// Issue #3760: Validate move using Arbitro Agent with AI-powered arbitration.
    /// POST /api/v1/agents/arbitro/validate
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

            var command = new ValidateMoveCommand
            {
                GameSessionId = req.GameSessionId,
                PlayerName = req.PlayerName,
                Action = req.Action,
                Position = req.Position,
                AdditionalContext = req.AdditionalContext
            };

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Arbitro validation: session={SessionId}, decision={Decision}, confidence={Confidence:F2}, latency={Latency}ms",
                req.GameSessionId, result.Decision, result.Confidence, result.LatencyMs);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("ArbitroValidateMove")
        .WithTags("Agents", "Arbitro")
        .Produces<MoveValidationResultDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(500);
    }
}

/// <summary>
/// Request for Arbitro Agent move validation.
/// Issue #3760.
/// </summary>
internal record ValidateMoveRequest(
    Guid GameSessionId,
    string PlayerName,
    string Action,
    string? Position = null,
    Dictionary<string, string>? AdditionalContext = null
);
