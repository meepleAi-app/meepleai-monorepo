using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
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
        var group = app.MapGroup("/api/v1/admin/queue")
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
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetProcessingQueueQuery(
            StatusFilter: status,
            SearchText: search,
            FromDate: fromDate,
            ToDate: toDate,
            Page: page ?? 1,
            PageSize: pageSize ?? 20);
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

}

// Request DTOs (defined alongside the routing file, following AdminPdfManagementEndpoints pattern)
internal record EnqueuePdfRequest(Guid PdfDocumentId, int Priority = 0);
internal record ReorderQueueRequest(List<Guid> OrderedJobIds);
internal record EnqueuePdfResponse(Guid JobId);
