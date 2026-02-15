using System.ComponentModel.DataAnnotations;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.Decisore;
using Api.Extensions;
using Api.Filters;
using Api.Infrastructure.Serialization;
using Api.Models;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Decisore Agent endpoints for strategic move suggestions.
/// Issue #3769: REST API for chess position analysis.
/// Issue #4334: Enhanced with SSE streaming for real-time analysis progress.
/// </summary>
internal static class DecisoreAgentEndpoints
{
    public static void MapDecisoreAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agents/decisore")
            .WithTags("Agents", "Decisore")
            .AddEndpointFilter<RequireSessionFilter>()
            .AddEndpointFilter<DecisoreRateLimitFilter>();  // Issue #4334: Rate limiting (10 req/min)

        // POST /api/v1/agents/decisore/analyze (SSE streaming)
        group.MapPost("/analyze", async (
            AnalyzeRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Set SSE headers
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            logger.LogInformation(
                "Decisore SSE analyze request: session={Session}, player={Player}, depth={Depth}",
                request.GameSessionId,
                request.PlayerName,
                request.AnalysisDepth);

            var command = new AnalyzeGameStateCommand(
                GameSessionId: request.GameSessionId,
                PlayerName: request.PlayerName,
                AnalysisDepth: request.AnalysisDepth ?? "standard",
                MaxSuggestions: request.MaxSuggestions ?? 3);

            try
            {
                await foreach (var @event in mediator.CreateStream(command, ct).ConfigureAwait(false))
                {
                    await context.Response.WriteAsync(
                        $"data: {System.Text.Json.JsonSerializer.Serialize(@event, SseJsonOptions.Default)}\n\n",
                        ct).ConfigureAwait(false);

                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }

                logger.LogInformation("Decisore SSE analysis stream completed");
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex, "Decisore analysis cancelled: session={Session}", request.GameSessionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Decisore analysis stream: session={Session}", request.GameSessionId);

                var errorEvent = new RagStreamingEvent(
                    StreamingEventType.Error,
                    new StreamingError(ex.Message, "ANALYSIS_ERROR"),
                    DateTime.UtcNow);

                await context.Response.WriteAsync(
                    $"data: {System.Text.Json.JsonSerializer.Serialize(errorEvent, SseJsonOptions.Default)}\n\n",
                    ct).ConfigureAwait(false);

                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        })
        .WithName("DecisoreAnalyze")
        .WithSummary("Analyze chess position and suggest strategic moves (SSE streaming)")
        .WithDescription(@"
Analyzes chess position with Decisore Agent and streams real-time progress via SSE.

**Flow**:
1. Load game session and verify player membership
2. Parse current board state
3. Generate candidate moves
4. Evaluate moves with multi-model ensemble (if deep analysis)
5. Return strategic suggestions with reasoning

**SSE Events**:
- `StateUpdate`: Progress messages
- `Complete`: Final StrategicAnalysisResultDto with suggestions

**Rate Limiting**: 10 expert analyses per minute

**Performance**: <5s P95 for expert analysis, <10s for deep (multi-model ensemble)
");
    }
}


internal sealed record AnalyzeRequest(
    Guid GameSessionId,
    string PlayerName,
    string? AnalysisDepth = "standard",
    int? MaxSuggestions = 3);
