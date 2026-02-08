using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Extensions;
using System.Text.Json;

namespace Api.Routing;

/// <summary>
/// Real-time batch job logs via SSE (Issue #3693 - Task 3)
/// </summary>
internal static class BatchJobLogsEndpoints
{
    public static RouteGroupBuilder MapBatchJobLogsEndpoints(this RouteGroupBuilder group)
    {
        var logsGroup = group.MapGroup("/admin/operations/batch-jobs")
            .WithTags("Admin - Batch Job Logs");

        // GET /api/v1/admin/operations/batch-jobs/{id}/logs/stream - SSE logs
        logsGroup.MapGet("/{id:guid}/logs/stream", HandleStreamLogs)
            .WithName("StreamBatchJobLogs")
            .WithSummary("Stream real-time job logs via SSE")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");

        return group;
    }

    private static async Task HandleStreamLogs(
        Guid id,
        HttpContext context,
        IBatchJobRepository repository,
        CancellationToken ct)
    {
        var (authorized, _, _) = context.RequireAdminSession();
        if (!authorized)
        {
            context.Response.StatusCode = 401;
            return;
        }

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

        // Stream logs while job is running
        while (!ct.IsCancellationRequested)
        {
            var job = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);

            if (job == null)
            {
                await SendEventAsync(context, "error", "Job not found", ct).ConfigureAwait(false);
                break;
            }

            // Send progress update
            var progressData = new
            {
                status = job.Status.ToString(),
                progress = job.Progress,
                timestamp = DateTime.UtcNow
            };

            await SendEventAsync(context, "progress", JsonSerializer.Serialize(progressData), ct).ConfigureAwait(false);

            // Stop streaming if job finished
            if (job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
            {
                await SendEventAsync(context, "complete", $"Job {job.Status}", ct).ConfigureAwait(false);
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
    }

    private static async Task SendEventAsync(HttpContext context, string eventType, string data, CancellationToken ct)
    {
        var message = $"event: {eventType}\ndata: {data}\n\n";
        await context.Response.WriteAsync(message, ct).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}
