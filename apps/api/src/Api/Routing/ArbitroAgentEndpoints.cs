using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Arbitro agent endpoints for real-time move validation.
/// Issue #3760: Arbitro Agent Move Validation Logic with Game State Analysis.
/// Issue #3762: REST API Endpoint - Full Integration with OpenAPI documentation.
/// </summary>
internal static class ArbitroAgentEndpoints
{
    public static RouteGroupBuilder MapArbitroAgentEndpoints(this RouteGroupBuilder group)
    {
        MapValidateMoveEndpoint(group);

        return group;
    }

    /// <summary>
    /// Validate a player move using Arbitro Agent with AI-powered rules arbitration.
    /// </summary>
    /// <remarks>
    /// The Arbitro Agent analyzes game state, retrieves applicable rules, and uses AI reasoning
    /// to determine if a proposed move is valid, invalid, or uncertain. Returns structured
    /// validation result with confidence score, reasoning, and suggestions.
    ///
    /// Performance: ~200-500ms total end-to-end (LLM inference ~200-400ms + overhead &lt;100ms P95).
    ///
    /// Note: SSE streaming intentionally NOT implemented - validation completes quickly with
    /// no intermediate steps to stream. For operations this fast, sync response is more efficient.
    /// </remarks>
    /// <response code="200">Move validation completed successfully</response>
    /// <response code="400">Invalid request (validation errors)</response>
    /// <response code="401">Unauthorized (session required)</response>
    /// <response code="404">Game session not found</response>
    /// <response code="500">Server error during validation</response>
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
        .WithSummary("Validate player move with AI-powered rules arbitration")
        .WithDescription(@"
Validates a player move against game rules using the Arbitro Agent's AI reasoning.

**Flow**:
1. Loads game session and verifies player membership
2. Retrieves current game state from snapshots
3. Extracts applicable rules from game's RuleSpec
4. Assembles AI prompt with state + rules + move context
5. Uses LLM to determine validity with confidence scoring

**Response**:
- `Decision`: VALID | INVALID | UNCERTAIN
- `Confidence`: 0.0-1.0 score
- `Reasoning`: AI explanation (max 200 chars)
- `ViolatedRules`: List of rule keys if invalid
- `Suggestions`: Alternative moves if invalid
- `ApplicableRules`: Rules considered during validation

**Performance**: ~200-500ms total (LLM ~200-400ms + overhead &lt;100ms P95)
")
        .Produces<MoveValidationResultDto>(200, "application/json", "Move validation completed")
        .ProducesProblem(400, "application/problem+json")
        .ProducesValidationProblem(400, "application/problem+json")
        .ProducesProblem(401, "application/problem+json")
        .ProducesProblem(404, "application/problem+json")
        .ProducesProblem(500, "application/problem+json");
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
