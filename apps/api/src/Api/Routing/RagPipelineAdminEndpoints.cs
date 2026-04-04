using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using Api.BoundedContexts.Administration.Application.Queries.RagPipeline;
using Api.Extensions;
using Api.Helpers;
using Api.Middleware;
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

        // Strategy CRUD endpoints (Issue #3464)
        MapStrategyEndpoints(ragGroup);

        return group;
    }

    /// <summary>
    /// CRUD endpoints for RAG pipeline strategies.
    /// Issue #3464: Save/load/export for custom strategies.
    /// </summary>
    private static void MapStrategyEndpoints(RouteGroupBuilder group)
    {
        var strategiesGroup = group.MapGroup("/strategies");

        // GET /api/v1/admin/rag-pipeline/strategies - List strategies
        strategiesGroup.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] bool includeTemplates,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetRagPipelineStrategiesQuery(
                session!.User!.Id,
                search,
                includeTemplates);

            var strategies = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(strategies);
        })
        .WithName("GetRagPipelineStrategies")
        .WithSummary("List RAG pipeline strategies")
        .WithDescription("Returns all strategies owned by the user plus templates.")
        .Produces<IReadOnlyList<RagPipelineStrategyDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/rag-pipeline/strategies/{id} - Get single strategy
        strategiesGroup.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetRagPipelineStrategyByIdQuery(id, session!.User!.Id);
            var strategy = await mediator.Send(query, ct).ConfigureAwait(false);

            return strategy != null
                ? Results.Ok(strategy)
                : Results.NotFound(new { message = "Strategy not found" });
        })
        .WithName("GetRagPipelineStrategyById")
        .WithSummary("Get a RAG pipeline strategy by ID")
        .WithDescription("Returns full strategy details including nodes and edges.")
        .Produces<RagPipelineStrategyDetailDto>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/rag-pipeline/strategies - Create new strategy
        strategiesGroup.MapPost("/", async (
            [FromBody] SaveStrategyRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new SaveRagPipelineStrategyCommand(
                null, // No ID for create
                request.Name,
                request.Description,
                request.NodesJson,
                request.EdgesJson,
                session!.User!.Id,
                request.Tags);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Admin {AdminId} created RAG pipeline strategy {StrategyId}: {Name}",
                session.User.Id,
                result.Id,
                LogSanitizer.Sanitize(result.Name));

            return Results.Created($"/api/v1/admin/rag-pipeline/strategies/{result.Id}", result);
        })
        .WithName("CreateRagPipelineStrategy")
        .WithSummary("Create a new RAG pipeline strategy")
        .WithDescription("Saves a new pipeline strategy to the database.")
        .Produces<SaveRagPipelineStrategyResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/rag-pipeline/strategies/{id} - Update strategy
        strategiesGroup.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] SaveStrategyRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new SaveRagPipelineStrategyCommand(
                id,
                request.Name,
                request.Description,
                request.NodesJson,
                request.EdgesJson,
                session!.User!.Id,
                request.Tags);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Admin {AdminId} updated RAG pipeline strategy {StrategyId}: {Name}",
                session.User.Id,
                result.Id,
                LogSanitizer.Sanitize(result.Name));

            return Results.Ok(result);
        })
        .WithName("UpdateRagPipelineStrategy")
        .WithSummary("Update a RAG pipeline strategy")
        .WithDescription("Updates an existing pipeline strategy.")
        .Produces<SaveRagPipelineStrategyResult>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/rag-pipeline/strategies/{id} - Delete strategy
        strategiesGroup.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new DeleteRagPipelineStrategyCommand(id, session!.User!.Id);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Admin {AdminId} deleted RAG pipeline strategy {StrategyId}",
                session.User.Id,
                id);

            return Results.NoContent();
        })
        .WithName("DeleteRagPipelineStrategy")
        .WithSummary("Delete a RAG pipeline strategy")
        .WithDescription("Soft-deletes a pipeline strategy.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/rag-pipeline/strategies/{id}/export - Export strategy as JSON file
        strategiesGroup.MapGet("/{id:guid}/export", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetRagPipelineStrategyByIdQuery(id, session!.User!.Id);
            var strategy = await mediator.Send(query, ct).ConfigureAwait(false);

            if (strategy == null)
            {
                return Results.NotFound(new { message = "Strategy not found" });
            }

            var export = new ExportedStrategy(
                strategy.Name,
                strategy.Description,
                strategy.Version,
                strategy.NodesJson,
                strategy.EdgesJson,
                strategy.Tags,
                DateTime.UtcNow);

            var json = JsonSerializer.Serialize(export, JsonOptions);
            var fileName = $"{SanitizeFileName(strategy.Name)}-{strategy.Version}.json";

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(json),
                "application/json",
                fileName);
        })
        .WithName("ExportRagPipelineStrategy")
        .WithSummary("Export a RAG pipeline strategy as JSON")
        .WithDescription("Downloads the strategy as a JSON file for backup or sharing.")
        .Produces(StatusCodes.Status200OK, contentType: "application/json")
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/rag-pipeline/strategies/import - Import strategy from JSON
        strategiesGroup.MapPost("/import", async (
            [FromBody] ImportStrategyRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Parse the imported JSON
            ExportedStrategy imported;
            try
            {
                imported = JsonSerializer.Deserialize<ExportedStrategy>(request.JsonContent, JsonOptions)
                    ?? throw new InvalidOperationException("Failed to parse strategy JSON");
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = $"Invalid strategy JSON: {ex.Message}" });
            }

            // Create new strategy from import
            var command = new SaveRagPipelineStrategyCommand(
                null,
                request.NewName ?? imported.Name,
                imported.Description,
                imported.NodesJson,
                imported.EdgesJson,
                session!.User!.Id,
                imported.Tags);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Admin {AdminId} imported RAG pipeline strategy {StrategyId}: {Name}",
                session.User.Id,
                result.Id,
                LogSanitizer.Sanitize(result.Name));

            return Results.Created($"/api/v1/admin/rag-pipeline/strategies/{result.Id}", result);
        })
        .WithName("ImportRagPipelineStrategy")
        .WithSummary("Import a RAG pipeline strategy from JSON")
        .WithDescription("Creates a new strategy from an exported JSON file.")
        .Produces<SaveRagPipelineStrategyResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
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
                LogValueSanitizer.Sanitize(request.TestQuery));

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

/// <summary>
/// Request DTO for saving a RAG pipeline strategy.
/// </summary>
internal sealed record SaveStrategyRequest(
    string Name,
    string Description,
    string NodesJson,
    string EdgesJson,
    IEnumerable<string>? Tags
);

/// <summary>
/// Request DTO for importing a RAG pipeline strategy.
/// </summary>
internal sealed record ImportStrategyRequest(
    string JsonContent,
    string? NewName
);

/// <summary>
/// DTO for exported strategy JSON format.
/// </summary>
internal sealed record ExportedStrategy(
    string Name,
    string Description,
    string Version,
    string NodesJson,
    string EdgesJson,
    IReadOnlyList<string> Tags,
    DateTime ExportedAt
);
