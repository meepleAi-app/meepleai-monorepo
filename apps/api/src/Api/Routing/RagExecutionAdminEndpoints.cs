using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands.RagExecution;
using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using Api.BoundedContexts.Administration.Application.Queries.RagExecution;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for RAG execution replay and comparison.
/// Issue #4459: RAG Query Replay.
/// </summary>
internal static class RagExecutionAdminEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RouteGroupBuilder MapRagExecutionAdminEndpoints(this RouteGroupBuilder group)
    {
        var execGroup = group.MapGroup("/admin/rag-executions")
            .WithTags("Admin - RAG Execution Replay");

        // POST /api/v1/admin/rag-executions/{id}/replay - Replay with SSE streaming
        MapReplayEndpoint(execGroup);

        // POST /api/v1/admin/rag-executions/compare - Side-by-side comparison
        MapCompareEndpoint(execGroup);

        return group;
    }

    /// <summary>
    /// SSE endpoint for replaying a past RAG execution.
    /// Re-executes the pipeline with original or overridden configuration.
    /// </summary>
    private static void MapReplayEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/replay", async (
            Guid id,
            [FromBody] ReplayExecutionRequest? request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {AdminId} replaying RAG execution {ExecutionId}",
                session!.User!.Id,
                id);

            IAsyncEnumerable<RagPipelineTestEvent> eventStream;
            try
            {
                var command = new ReplayRagExecutionCommand(
                    id,
                    session.User.Id,
                    request?.Strategy,
                    request?.TopK,
                    request?.Model,
                    request?.Temperature);

                eventStream = await mediator.Send(command, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start execution replay for {ExecutionId}", id);
                return Results.Problem(
                    title: "Replay Failed",
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
                logger.LogInformation(oce, "Execution replay cancelled by client for {ExecutionId}", id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error streaming replay events for {ExecutionId}", id);
            }

            return Results.Empty;
        })
        .WithName("ReplayRagExecution")
        .WithSummary("Replay a past RAG execution with SSE streaming")
        .WithDescription(@"Re-executes a stored RAG pipeline execution with optional configuration overrides.
Results are streamed via Server-Sent Events in the same format as the pipeline test endpoint.
The replay result is automatically persisted as a new execution linked to the original.

**Config Overrides** (all optional):
- `strategy`: Override the retrieval strategy
- `topK`: Override the number of documents to retrieve
- `model`: Override the LLM model
- `temperature`: Override the model temperature

**Event Types** (same as pipeline test):
- `PipelineTestStartedEvent`: Replay initialization
- `BlockExecutionStartedEvent`: Block starts executing
- `BlockExecutionCompletedEvent`: Block execution result
- `DocumentsRetrievedEvent`: Retrieved documents
- `ValidationResultEvent`: Validation results
- `PipelineTestCompletedEvent`: Final replay summary")
        .Produces(200)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Endpoint for comparing two RAG executions side-by-side.
    /// </summary>
    private static void MapCompareEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/compare", async (
            [FromBody] CompareExecutionsRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {AdminId} comparing executions {Exec1} and {Exec2}",
                session!.User!.Id,
                request.ExecutionIds[0],
                request.ExecutionIds[1]);

            try
            {
                var query = new CompareRagExecutionsQuery(
                    request.ExecutionIds[0],
                    request.ExecutionIds[1],
                    session.User.Id);

                var comparison = await mediator.Send(query, ct).ConfigureAwait(false);
                return Results.Ok(comparison);
            }
            catch (Api.Middleware.Exceptions.NotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("CompareRagExecutions")
        .WithSummary("Compare two RAG executions side-by-side")
        .WithDescription(@"Returns a detailed comparison of two RAG execution traces including:
- Execution summaries with metrics
- Metrics delta (duration, tokens, cost differences)
- Block-by-block comparison with status (improved/degraded/unchanged)
- Document selection diffs (added/removed documents, score changes)")
        .Produces<RagExecutionComparisonDto>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

/// <summary>
/// Request DTO for replaying a RAG execution.
/// All fields are optional - if omitted, original config is used.
/// </summary>
internal sealed record ReplayExecutionRequest(
    string? Strategy,
    int? TopK,
    string? Model,
    double? Temperature
);

/// <summary>
/// Request DTO for comparing two executions.
/// </summary>
internal sealed record CompareExecutionsRequest(
    Guid[] ExecutionIds
);
