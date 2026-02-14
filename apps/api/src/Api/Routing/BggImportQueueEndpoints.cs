using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// API endpoints for BGG import queue management.
/// Issue #3541: BGG Import Queue Service
/// Admin-only endpoints for monitoring and controlling the background import queue.
/// </summary>
internal static class BggImportQueueEndpoints
{
    internal static IEndpointRouteBuilder MapBggImportQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/bgg-queue")
            .WithTags("BGG Import Queue")
            .RequireAuthorization(policy => policy.RequireRole("Admin")); // Admin-only endpoints

        // GET /api/v1/admin/bgg-queue/status - Get current queue status
        group.MapGet("/status", GetQueueStatus)
            .WithName("GetBggQueueStatus")
            .WithSummary("Get current BGG import queue status")
            .WithDescription("Returns all queued and processing BGG import jobs with position information")
            .Produces<BggQueueStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/bgg-queue/enqueue - Enqueue single BGG ID
        group.MapPost("/enqueue", EnqueueSingle)
            .WithName("EnqueueBggImport")
            .WithSummary("Enqueue a single BGG game for import")
            .WithDescription("Add a BGG game ID to the import queue")
            .Produces<BggImportQueueEntity>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/bgg-queue/batch - Enqueue multiple BGG IDs
        group.MapPost("/batch", EnqueueBatch)
            .WithName("EnqueueBggBatch")
            .WithSummary("Enqueue multiple BGG games for import")
            .WithDescription("Add multiple BGG game IDs to the import queue in a single request")
            .Produces<List<BggImportQueueEntity>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/bgg-queue/batch-json - Bulk import from JSON file
        group.MapPost("/batch-json", EnqueueBatchFromJson)
            .WithName("EnqueueBggBatchFromJson")
            .WithSummary("Bulk import games from JSON content")
            .WithDescription("Parse JSON array of {bggId, name} objects, check duplicates, and enqueue new games with best-effort strategy (Issue #4352)")
            .Produces<BulkImportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/bgg-queue/{id} - Cancel queued import
        group.MapDelete("/{id:guid}", CancelQueuedImport)
            .WithName("CancelBggImport")
            .WithSummary("Cancel a queued BGG import")
            .WithDescription("Remove a queued BGG import job (only works for Queued status)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/bgg-queue/{id}/retry - Retry failed import
        group.MapPost("/{id:guid}/retry", RetryFailedImport)
            .WithName("RetryBggImport")
            .WithSummary("Retry a failed BGG import")
            .WithDescription("Reset a failed BGG import job and re-queue it")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/bgg-queue/{bggId} - Get queue entry by BGG ID
        group.MapGet("/{bggId:int}", GetByBggId)
            .WithName("GetBggQueueEntry")
            .WithSummary("Get queue entry by BGG ID")
            .WithDescription("Retrieve queue status for a specific BGG game ID")
            .Produces<BggImportQueueEntity>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/bgg-queue/stream - SSE stream for real-time queue progress
        group.MapGet("/stream", StreamQueueProgress)
            .WithName("StreamBggQueueProgress")
            .WithSummary("Server-Sent Events stream for queue progress")
            .WithDescription("Real-time updates on queue status changes (new items, completed, failed)")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> GetQueueStatus(
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetQueueStatusQuery();
        var queuedItems = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        var response = new BggQueueStatusResponse
        {
            TotalQueued = queuedItems.Count(q => q.Status == BggImportStatus.Queued),
            TotalProcessing = queuedItems.Count(q => q.Status == BggImportStatus.Processing),
            Items = queuedItems
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> EnqueueSingle(
        [FromBody] EnqueueBggRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new EnqueueBggCommand
            {
                BggId = request.BggId,
                GameName = request.GameName
            };

            var entity = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            return Results.Created($"/api/v1/admin/bgg-queue/{entity.Id}", entity);
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Duplicate BGG ID",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static async Task<IResult> EnqueueBatch(
        [FromBody] EnqueueBggBatchRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new EnqueueBggBatchCommand
        {
            BggIds = request.BggIds
        };

        var entities = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Created("/api/v1/admin/bgg-queue/status", entities);
    }

    private static async Task<IResult> EnqueueBatchFromJson(
        [FromBody] EnqueueBggBatchFromJsonRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = request.JsonContent,
            UserId = userId != null ? Guid.Parse(userId) : Guid.Empty
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> CancelQueuedImport(
        [FromRoute] Guid id,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CancelQueuedImportCommand(id);
        var cancelled = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        if (!cancelled)
            return Results.NotFound(new ProblemDetails
            {
                Title = "Queue Entry Not Found",
                Detail = $"Queue entry {id} not found or cannot be cancelled (must be in Queued status)",
                Status = StatusCodes.Status404NotFound
            });

        return Results.NoContent();
    }

    private static async Task<IResult> RetryFailedImport(
        [FromRoute] Guid id,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new RetryFailedImportCommand(id);
        var retried = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        if (!retried)
            return Results.NotFound(new ProblemDetails
            {
                Title = "Queue Entry Not Found",
                Detail = $"Queue entry {id} not found or cannot be retried (must be in Failed status)",
                Status = StatusCodes.Status404NotFound
            });

        return Results.NoContent();
    }

    private static async Task<IResult> GetByBggId(
        [FromRoute] int bggId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetByBggIdQuery(bggId);
        var entity = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        if (entity == null)
            return Results.NotFound(new ProblemDetails
            {
                Title = "Queue Entry Not Found",
                Detail = $"No queue entry found for BGG ID {bggId}",
                Status = StatusCodes.Status404NotFound
            });

        return Results.Ok(entity);
    }

    private static async Task StreamQueueProgress(
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Issue #3543 - Fix #3: Get all queue items to support completed/failed counts
                var query = new GetAllQueueItemsQuery();
                var allQueueItems = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

                var queuedCount = allQueueItems.Count(q => q.Status == BggImportStatus.Queued);
                var processingCount = allQueueItems.Count(q => q.Status == BggImportStatus.Processing);
                var completedCount = allQueueItems.Count(q => q.Status == BggImportStatus.Completed);
                var failedCount = allQueueItems.Count(q => q.Status == BggImportStatus.Failed);

                // Send only queued/processing items in the items array
                var activeItems = allQueueItems
                    .Where(q => q.Status == BggImportStatus.Queued || q.Status == BggImportStatus.Processing)
                    .Take(10)
                    .ToList();

                // Send SSE event
                var eventData = new
                {
                    timestamp = DateTime.UtcNow,
                    queued = queuedCount,
                    processing = processingCount,
                    completed = completedCount,
                    failed = failedCount,
                    eta = queuedCount + processingCount, // Estimate in seconds (1 req/sec)
                    items = activeItems // Send only top 10 active items for performance
                };

                await httpContext.Response.WriteAsync(
                    $"data: {System.Text.Json.JsonSerializer.Serialize(eventData)}\n\n",
                    cancellationToken).ConfigureAwait(false);

                await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Update every 2 seconds (balance between real-time and server load)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - expected
        }
    }
}

/// <summary>
/// Response for queue status endpoint
/// </summary>
public sealed class BggQueueStatusResponse
{
    public int TotalQueued { get; init; }
    public int TotalProcessing { get; init; }
    public List<BggImportQueueEntity> Items { get; init; } = [];
}

/// <summary>
/// Request to enqueue a single BGG game
/// </summary>
public sealed record EnqueueBggRequest(int BggId, string? GameName = null);

/// <summary>
/// Request to enqueue multiple BGG games
/// </summary>
public sealed record EnqueueBggBatchRequest(List<int> BggIds);

/// <summary>
/// Request to bulk import games from JSON content (Issue #4352)
/// </summary>
public sealed record EnqueueBggBatchFromJsonRequest(string JsonContent);
