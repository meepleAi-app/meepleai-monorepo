using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Extensions;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for PDF processing queue management.
/// Issue #4731: Queue commands/queries API.
/// </summary>
internal static class AdminQueueEndpoints
{
    public static void MapAdminQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/queue")
            .WithTags("Admin - Processing Queue")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/enqueue", HandleEnqueue)
            .WithName("EnqueuePdf")
            .Produces<EnqueuePdfResponse>(201)
            .Produces(404)
            .Produces(409)
            .WithSummary("Add a PDF document to the processing queue");

        group.MapDelete("/{jobId:guid}", HandleRemove)
            .WithName("RemoveFromQueue")
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithSummary("Remove a queued job (only Queued status)");

        group.MapPost("/{jobId:guid}/cancel", HandleCancel)
            .WithName("CancelJob")
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithSummary("Cancel a queued or processing job");

        group.MapPost("/{jobId:guid}/retry", HandleRetry)
            .WithName("RetryJob")
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithSummary("Retry a failed job");

        group.MapPut("/reorder", HandleReorder)
            .WithName("ReorderQueue")
            .Produces(204)
            .Produces(409)
            .WithSummary("Reorder queued jobs by priority (drag-and-drop)");

        group.MapGet("/", HandleGetQueue)
            .WithName("GetProcessingQueue")
            .Produces<PaginatedQueueResponse>(200)
            .WithSummary("Get paginated processing queue with filters");

        group.MapGet("/{jobId:guid}", HandleGetJobDetail)
            .WithName("GetJobDetail")
            .Produces<ProcessingJobDetailDto>(200)
            .Produces(404)
            .WithSummary("Get job detail with steps and log entries");

        // Issue #4732: SSE streaming endpoints
        group.MapGet("/{jobId:guid}/stream", HandleStreamJobUpdates)
            .WithName("StreamJobUpdates")
            .Produces(200, contentType: "text/event-stream")
            .Produces(404)
            .WithSummary("SSE stream for a single job's real-time updates");

        group.MapGet("/stream", HandleStreamQueueUpdates)
            .WithName("StreamQueueUpdates")
            .Produces(200, contentType: "text/event-stream")
            .WithSummary("SSE stream for queue-wide real-time updates");

        // Issue #5455: Priority bump endpoint
        group.MapPatch("/{jobId:guid}/priority", HandleBumpPriority)
            .WithName("BumpJobPriority")
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .WithSummary("Bump the priority of a queued job");

        // Issue #5455: Queue configuration endpoints
        group.MapGet("/config", HandleGetQueueConfig)
            .WithName("GetQueueConfig")
            .Produces<QueueConfigDto>(200)
            .WithSummary("Get current queue configuration");

        group.MapPatch("/config", HandleUpdateQueueConfig)
            .WithName("UpdateQueueConfig")
            .Produces(204)
            .WithSummary("Update queue configuration (pause/resume, concurrency)");

        // Issue #5456: Bulk reindex failed documents
        group.MapPost("/reindex-failed", HandleBulkReindexFailed)
            .WithName("BulkReindexFailed")
            .Produces<BulkReindexResult>(200)
            .WithSummary("Re-queue all failed documents as Low priority");

        // Issue #5456: Extracted text preview
        group.MapGet("/documents/{pdfDocumentId:guid}/extracted-text", HandleGetExtractedText)
            .WithName("GetExtractedText")
            .Produces<PdfTextResult>(200)
            .Produces(404)
            .WithSummary("Preview extracted text for a document before embedding");

        // Issue #5457: Queue status with backpressure info
        group.MapGet("/status", HandleGetQueueStatus)
            .WithName("GetQueueStatus")
            .Produces<QueueStatusDto>(200)
            .WithSummary("Get queue status with depth, ETA, and backpressure info");

        // Issue #5459: Dashboard metrics with period filtering
        group.MapGet("/metrics", HandleGetDashboardMetrics)
            .WithName("GetDashboardMetrics")
            .Produces<DashboardMetricsDto>(200)
            .WithSummary("Get processing metrics for dashboard (period: 24h, 7d, 30d)");

        // Issue #5460: Active alerts for the processing queue
        group.MapGet("/alerts", HandleGetActiveAlerts)
            .WithName("GetActiveAlerts")
            .Produces<IReadOnlyList<QueueAlertDto>>(200)
            .WithSummary("Get currently active queue alerts (stuck docs, depth, failure rate)");
    }

    private static async Task<IResult> HandleEnqueue(
        EnqueuePdfRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Filter already validates admin session; extract user ID from session
        var (_, session, _) = context.RequireAdminSession();
        var userId = session.User!.Id;

        var command = new EnqueuePdfCommand(request.PdfDocumentId, userId, request.Priority);
        var jobId = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/admin/queue/{jobId}", new EnqueuePdfResponse(jobId));
    }

    private static async Task<IResult> HandleRemove(
        Guid jobId,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new RemoveFromQueueCommand(jobId), ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleCancel(
        Guid jobId,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new CancelJobCommand(jobId), ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleRetry(
        Guid jobId,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new RetryJobCommand(jobId), ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleReorder(
        ReorderQueueRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new ReorderQueueCommand(request.OrderedJobIds), ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleGetQueue(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] Guid? gameId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetProcessingQueueQuery(
            StatusFilter: status,
            SearchText: search,
            FromDate: fromDate,
            ToDate: toDate,
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            GameId: gameId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetJobDetail(
        Guid jobId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetJobDetailQuery(jobId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// Issue #4732: SSE stream for a single job.
    /// Streams real-time updates (step progress, completion, failure) until terminal event.
    /// </summary>
    private static async Task HandleStreamJobUpdates(
        Guid jobId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        var query = new StreamJobUpdatesQuery(jobId);

        try
        {
            await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
            {
                var eventName = evt.Type.ToString();
                var json = JsonSerializer.Serialize(evt);

                await httpContext.Response.WriteAsync($"event: {eventName}\n", ct).ConfigureAwait(false);
                await httpContext.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - expected behavior
        }
    }

    /// <summary>
    /// Issue #4732: SSE stream for the entire queue.
    /// Streams all job events and queue-wide events to admin dashboard.
    /// </summary>
    private static async Task HandleStreamQueueUpdates(
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        var query = new StreamQueueUpdatesQuery();

        try
        {
            await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
            {
                var eventName = evt.Type.ToString();
                var json = JsonSerializer.Serialize(evt);

                await httpContext.Response.WriteAsync($"event: {eventName}\n", ct).ConfigureAwait(false);
                await httpContext.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - expected behavior
        }
    }

    private static async Task<IResult> HandleBumpPriority(
        Guid jobId,
        BumpPriorityRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new BumpPriorityCommand(jobId, request.NewPriority), ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleGetQueueConfig(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetQueueConfigQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateQueueConfig(
        UpdateQueueConfigRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (_, session, _) = context.RequireAdminSession();
        var userId = session.User!.Id;

        await mediator.Send(
            new UpdateQueueConfigCommand(userId, request.IsPaused, request.MaxConcurrentWorkers), ct)
            .ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleBulkReindexFailed(
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (_, session, _) = context.RequireAdminSession();
        var userId = session.User!.Id;

        var result = await mediator.Send(new BulkReindexFailedCommand(userId), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetExtractedText(
        Guid pdfDocumentId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetPdfTextQuery(pdfDocumentId), ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> HandleGetQueueStatus(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetQueueStatusQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetDashboardMetrics(
        [FromQuery] string? period,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetDashboardMetricsQuery(period ?? "24h"), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetActiveAlerts(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetActiveAlertsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

// Request DTOs (defined alongside the routing file, following AdminPdfManagementEndpoints pattern)
internal record EnqueuePdfRequest(Guid PdfDocumentId, int Priority = 0);
internal record ReorderQueueRequest(List<Guid> OrderedJobIds);
internal record EnqueuePdfResponse(Guid JobId);
internal record BumpPriorityRequest(ProcessingPriority NewPriority);
internal record UpdateQueueConfigRequest(bool? IsPaused = null, int? MaxConcurrentWorkers = null);
