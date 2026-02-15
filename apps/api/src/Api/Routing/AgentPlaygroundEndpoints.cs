using System.ComponentModel.DataAnnotations;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Filters;
using Api.Infrastructure.Serialization;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Agent Playground endpoints for testing agents with real AgentDefinition integration.
/// Issue #4392: Replace placeholder with real SSE streaming via AgentDefinition pipeline.
/// Epic #3710: Agent Playground.
/// </summary>
internal static class AgentPlaygroundEndpoints
{
    public static void MapAgentPlaygroundEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/agent-definitions/{agentId:guid}/playground")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // POST /api/v1/admin/agent-definitions/{agentId}/playground/chat
        // SSE streaming chat endpoint with real AgentDefinition integration
        group.MapPost("/chat", async (
            Guid agentId,
            PlaygroundChatRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Set SSE headers
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            var command = new PlaygroundChatCommand(
                AgentDefinitionId: agentId,
                Message: request.Message,
                GameId: Guid.TryParse(request.GameId, out var gid) ? gid : null);

            try
            {
                await foreach (var @event in mediator.CreateStream(command, ct).ConfigureAwait(false))
                {
                    await context.Response.WriteAsync(
                        $"data: {System.Text.Json.JsonSerializer.Serialize(@event, SseJsonOptions.Default)}\n\n",
                        ct).ConfigureAwait(false);

                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex, "Playground chat cancelled for agent {AgentId}", agentId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in playground chat for agent {AgentId}", agentId);

                var errorEvent = new Api.Models.RagStreamingEvent(
                    Api.Models.StreamingEventType.Error,
                    new Api.Models.StreamingError(ex.Message, "INTERNAL_ERROR"),
                    DateTime.UtcNow);

                await context.Response.WriteAsync(
                    $"data: {System.Text.Json.JsonSerializer.Serialize(errorEvent, SseJsonOptions.Default)}\n\n",
                    ct).ConfigureAwait(false);

                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        })
        .WithName("AgentPlaygroundChat")
        .WithTags("Agent Playground", "Admin")
        .WithSummary("Test agent in playground with SSE streaming (real AgentDefinition)")
        .WithDescription(
            "Send message to agent and receive streaming response. " +
            "Uses real AgentDefinition config (model, temperature, prompts) for LLM generation. " +
            "Returns RagStreamingEvent format: StateUpdate → Token(s) → FollowUpQuestions → Complete.");
    }
}

internal sealed record PlaygroundChatRequest(
    [Required, MinLength(1), MaxLength(4000)] string Message,
    string? GameId = null);
