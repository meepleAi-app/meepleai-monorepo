using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for RAG pipeline builder operations.
/// Issue #3463: Live test API with SSE streaming.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
internal static class RagPipelineAdminEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RouteGroupBuilder MapRagPipelineAdminEndpoints(this RouteGroupBuilder group)
    {
        var ragGroup = group.MapGroup("/admin/rag-pipeline")
            .WithTags("Admin - RAG Pipeline Builder");

        // POST /api/v1/admin/rag-pipeline/test - Test pipeline with SSE streaming
        MapTestPipelineEndpoint(ragGroup);

        return group;
    }

    /// <summary>
    /// SSE endpoint for testing RAG pipelines with real-time streaming results.
    /// </summary>
    private static void MapTestPipelineEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/test", async (
            [FromBody] TestPipelineRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Require admin session
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {AdminId} testing RAG pipeline: query='{Query}'",
                session!.User!.Id,
                request.TestQuery);

            // Execute test command to get event stream
            IAsyncEnumerable<RagPipelineTestEvent> eventStream;
            try
            {
                var command = new TestRagPipelineCommand(
                    request.PipelineDefinition,
                    request.TestQuery,
                    session.User.Id);

                eventStream = await mediator.Send(command, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start pipeline test");
                return Results.Problem(
                    title: "Pipeline Test Failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            // Set SSE response headers
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            context.Response.Headers.Append("X-Accel-Buffering", "no");

            try
            {
                // Stream events to client
                await foreach (var evt in eventStream.ConfigureAwait(false))
                {
                    var eventType = evt.EventType;
                    var json = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);

                    await context.Response.WriteAsync($"event: {eventType}\n", ct).ConfigureAwait(false);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation(oce, "Pipeline test cancelled by client");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error streaming pipeline test events");
            }

            return Results.Empty;
        })
        .WithName("TestRagPipeline")
        .WithSummary("Test RAG pipeline with SSE streaming results")
        .WithDescription(@"Executes a RAG pipeline test and streams results via Server-Sent Events.

**Event Types**:
- `PipelineTestStartedEvent`: Test initialization
- `BlockExecutionStartedEvent`: Block starts executing
- `BlockExecutionCompletedEvent`: Block execution result
- `DocumentsRetrievedEvent`: Retrieved documents from search blocks
- `ValidationResultEvent`: Validation/evaluation results
- `PipelineTestCompletedEvent`: Final test summary

**Example**:
```javascript
const response = await fetch('/api/v1/admin/rag-pipeline/test', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  credentials: 'include',
  body: JSON.stringify({
    pipelineDefinition: JSON.stringify(pipeline),
    testQuery: 'How do I setup the game?'
  })
});

const reader = response.body.getReader();
const decoder = new TextDecoder();
// Process SSE events...
```")
        .Produces(200)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

/// <summary>
/// Request DTO for testing a RAG pipeline.
/// </summary>
internal sealed record TestPipelineRequest(
    string PipelineDefinition,
    string TestQuery
);
