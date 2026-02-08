using System.Text;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Agent Playground endpoints for testing agents (Issue #3810, Epic #3687)
/// </summary>
internal static class AgentPlaygroundEndpoints
{
    public static void MapAgentPlaygroundEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/agent-definitions/{agentId:guid}/playground");

        // POST /api/v1/admin/agent-definitions/{agentId}/playground/chat
        // SSE streaming chat endpoint
        group.MapPost("/chat", async (
            Guid agentId,
            PlaygroundChatRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            try
            {
                // Placeholder: Will integrate with AgentDefinition after #3850 merges
                await SendSseEventAsync(context.Response, "start", new { agentId, message = "Chat started" }, ct);

                // Simulate streaming response
                var response = $"Playground test response for agent {agentId}: {request.Message}";
                var chunks = response.Split(' ');

                foreach (var chunk in chunks)
                {
                    await SendSseEventAsync(context.Response, "chunk", new { content = chunk + " " }, ct);
                    await Task.Delay(50, ct); // Simulate streaming delay
                }

                // Send metadata
                await SendSseEventAsync(context.Response, "metadata", new
                {
                    tokens = 150,
                    latency = 1.2,
                    model = "gpt-4"
                }, ct);

                await SendSseEventAsync(context.Response, "done", new { success = true }, ct);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Playground chat cancelled for agent {AgentId}", agentId);
                await SendSseEventAsync(context.Response, "error", new { message = "Cancelled" }, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in playground chat for agent {AgentId}", agentId);
                await SendSseEventAsync(context.Response, "error", new { message = ex.Message }, ct);
            }

            return Results.Ok();
        })
        .WithName("AgentPlaygroundChat")
        .WithTags("Agent Playground", "Admin")
        .WithSummary("Test agent in playground with SSE streaming")
        .WithDescription("Send message to agent and receive streaming response");
    }

    private static async Task SendSseEventAsync(
        HttpResponse response,
        string eventType,
        object data,
        CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var message = $"event: {eventType}\ndata: {json}\n\n";
        await response.WriteAsync(message, Encoding.UTF8, ct);
        await response.Body.FlushAsync(ct);
    }
}

internal sealed record PlaygroundChatRequest(string Message);
