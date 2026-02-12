using Api.BoundedContexts.KnowledgeBase.Application.Commands.Decisore;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.Decisore;
using Api.Extensions;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Decisore Agent endpoints for strategic move suggestions.
/// Issue #3769: REST API for chess position analysis.
/// </summary>
internal static class DecisoreAgentEndpoints
{
    public static void MapDecisoreAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agents/decisore")
            .WithTags("Agents", "Decisore")
            .AddEndpointFilter<RequireSessionFilter>();

        // POST /api/v1/agents/decisore/analyze
        group.MapPost("/analyze", async (
            AnalyzeRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Decisore analyze request: session={Session}, player={Player}, depth={Depth}",
                request.GameSessionId,
                request.PlayerName,
                request.AnalysisDepth);

            var command = new AnalyzeGameStateCommand(
                GameSessionId: request.GameSessionId,
                PlayerName: request.PlayerName,
                AnalysisDepth: request.AnalysisDepth ?? "standard",
                MaxSuggestions: request.MaxSuggestions ?? 3);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Decisore analysis complete: suggestions={Count}, time={Time}ms",
                result.Suggestions.Count,
                result.ExecutionTimeMs);

            return Results.Ok(result);
        })
        .WithName("DecisoreAnalyze")
        .WithSummary("Analyze chess position and suggest strategic moves")
        .WithDescription("Returns AI-powered move suggestions with reasoning, position evaluation, and victory paths");
    }
}

internal sealed record AnalyzeRequest(
    Guid GameSessionId,
    string PlayerName,
    string? AnalysisDepth = "standard",
    int? MaxSuggestions = 3);
