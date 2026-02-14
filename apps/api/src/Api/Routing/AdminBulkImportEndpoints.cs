using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin bulk import endpoints with rate limiting and Swagger documentation.
/// Issue #4354: Backend - Bulk Import Endpoint Routing
/// Epic #4136: Admin Game Import
///
/// Consolidates bulk import operations under /api/v1/admin/games/bulk-import:
/// - POST / - Submit JSON for bulk import (rate limited: 1 req/5min)
/// - GET /progress - SSE stream for real-time progress tracking
/// </summary>
internal static class AdminBulkImportEndpoints
{
    internal static void MapAdminBulkImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/games/bulk-import")
            .WithTags("Admin - Bulk Import")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // POST /api/v1/admin/games/bulk-import - Submit bulk import from JSON
        group.MapPost("/", HandleBulkImport)
            .WithName("BulkImportGamesFromJson")
            .WithSummary("Bulk import games from JSON (Admin only, rate limited)")
            .WithDescription(
                "Parse JSON array of {bggId, name} objects, check for duplicates against existing catalog, " +
                "and enqueue new games for BGG import with best-effort strategy. " +
                "Rate limited to 1 request per 5 minutes per user. " +
                "Skipped duplicates and individual failures are reported in the response. (Issue #4354)")
            .Produces<BulkImportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting("BulkImportAdmin");

        // GET /api/v1/admin/games/bulk-import/progress - SSE stream for progress
        group.MapGet("/progress", HandleBulkImportProgress)
            .WithName("BulkImportProgress")
            .WithSummary("SSE stream for bulk import progress (Admin only)")
            .WithDescription(
                "Server-Sent Events stream providing real-time progress updates for bulk import operations. " +
                "Updates every 1 second. Auto-closes after 5 seconds of no active items. " +
                "Events: 'progress' (ongoing), 'complete' (finished). (Issue #4353)")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Handle bulk import: parse JSON, check duplicates, enqueue new games.
    /// Rate limited to 1 request per 5 minutes per user.
    /// </summary>
    private static async Task<IResult> HandleBulkImport(
        [FromBody] BulkImportFromJsonRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = request.JsonContent,
            UserId = userId
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    /// <summary>
    /// SSE endpoint for bulk import progress tracking.
    /// Updates every 1 second, auto-closes after 5 seconds of inactivity.
    /// </summary>
    private static async Task HandleBulkImportProgress(
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        var consecutiveIdlePolls = 0;
        const int maxIdlePolls = 5;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var query = new GetBulkImportProgressQuery();
                var progress = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

                await httpContext.Response.WriteAsync(
                    $"event: progress\ndata: {System.Text.Json.JsonSerializer.Serialize(progress)}\n\n",
                    cancellationToken).ConfigureAwait(false);

                await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

                if (!progress.IsActive)
                {
                    consecutiveIdlePolls++;
                    if (consecutiveIdlePolls >= maxIdlePolls)
                    {
                        await httpContext.Response.WriteAsync(
                            $"event: complete\ndata: {System.Text.Json.JsonSerializer.Serialize(progress)}\n\n",
                            cancellationToken).ConfigureAwait(false);

                        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    }
                }
                else
                {
                    consecutiveIdlePolls = 0;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - expected behavior
        }
    }
}

/// <summary>
/// Request model for JSON bulk import endpoint.
/// Accepts JSON content as a string containing an array of {bggId, name} objects.
/// Issue #4354
/// </summary>
public sealed record BulkImportFromJsonRequest(string JsonContent);
