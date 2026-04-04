using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using PdfIndexingErrorCode = Api.BoundedContexts.DocumentProcessing.Application.DTOs.PdfIndexingErrorCode;

namespace Api.Routing;

/// <summary>
/// PDF processing state and action endpoints.
/// Covers: progress, cancel, SSE streams, metrics, rulespec, index, extract, retry, batch-process-pending.
/// </summary>
internal static class PdfProcessingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapProcessingStateEndpoints(group);
        MapProcessingActionsEndpoints(group);
    }

    private static void MapProcessingStateEndpoints(RouteGroupBuilder group)
    {
        MapProcessingProgressEndpoint(group);
        MapProcessingCancelEndpoint(group);
        MapProcessingStatusStreamEndpoint(group); // Issue #4218: SSE streaming
        MapProgressStreamEndpoint(group); // Issue #4209: Progress stream for public PDFs
        MapMetricsEndpoint(group); // Issue #4219: Duration metrics and ETA
    }

    private static void MapProcessingProgressEndpoint(RouteGroupBuilder group)
    {
        // PDF-08: Get PDF processing progress
        group.MapGet("/pdfs/{pdfId:guid}/progress", HandleGetPdfProcessingProgress)
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetPdfProcessingProgress");
    }

    private static void MapProcessingCancelEndpoint(RouteGroupBuilder group)
    {
        // PDF-08: Cancel PDF processing
        group.MapDelete("/pdfs/{pdfId:guid}/processing", HandleCancelPdfProcessing)
        .RequireSession()
        .RequireAuthorization()
        .WithName("CancelPdfProcessing");
    }

    private static void MapProcessingStatusStreamEndpoint(RouteGroupBuilder group)
    {
        // Issue #4218: Real-time PDF status updates via Server-Sent Events
        group.MapGet("/pdfs/{pdfId:guid}/status/stream", HandleStreamPdfStatus)
        .RequireSession()
        .RequireAuthorization()
        .WithName("StreamPdfStatus")
        .WithDescription("Stream real-time PDF processing status updates via SSE");
    }

    /// <summary>
    /// Issue #4209: SSE endpoint for streaming PDF processing progress.
    /// Supports both public and private PDFs with owner/admin authorization.
    /// </summary>
    private static void MapProgressStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/pdfs/{pdfId:guid}/progress/stream", HandleStreamPdfProgress)
        .RequireSession()
        .RequireAuthorization()
        .Produces<ProcessingProgressJson>(200, contentType: "text/event-stream")
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithName("StreamPdfProgress")
        .WithTags("PDF", "SSE")
        .WithSummary("Stream PDF processing progress (SSE)")
        .WithDescription("Server-Sent Events endpoint for real-time progress updates during PDF processing. Supports public and private PDFs. Owner or admin access required. Heartbeat every 30s. Issue #4209.");
    }

    private static void MapMetricsEndpoint(RouteGroupBuilder group)
    {
        // Issue #4219: Get PDF processing metrics (timing, ETA, progress)
        group.MapGet("/documents/{id:guid}/metrics", HandleGetPdfMetrics)
        .RequireSession()
        .WithName("GetPdfMetrics")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get PDF processing metrics and ETA";
            operation.Description = "Retrieves detailed processing metrics including per-state timing, progress percentage, and estimated time remaining.";
            return operation;
        });
    }

    private static void MapProcessingActionsEndpoints(RouteGroupBuilder group)
    {
        MapPdfRuleSpecEndpoints(group);
        MapPdfProcessingEndpoints(group);
    }

    private static void MapPdfRuleSpecEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/ingest/pdf/{pdfId:guid}/rulespec", HandleGenerateRuleSpec);
    }

    private static void MapPdfProcessingEndpoints(RouteGroupBuilder group)
    {
        MapPdfIndexEndpoint(group);
        MapPdfExtractEndpoint(group);
        MapPdfRetryEndpoint(group); // Issue #4216: Manual retry
        MapBatchProcessPendingEndpoint(group); // Batch process all pending PDFs
    }

    private static void MapPdfIndexEndpoint(RouteGroupBuilder group)
    {
        // AI-01: Index PDF for semantic search
        group.MapPost("/ingest/pdf/{pdfId:guid}/index", HandleIndexPdf);
    }

    private static void MapPdfExtractEndpoint(RouteGroupBuilder group)
    {
        // BGAI-081: Extract text from existing PDF (reprocess stuck PDFs)
        group.MapPost("/ingest/pdf/{pdfId:guid}/extract", HandleExtractPdfText);
    }

    private static void MapPdfRetryEndpoint(RouteGroupBuilder group)
    {
        // Issue #4216: Retry failed PDF processing
        group.MapPost("/documents/{pdfId:guid}/retry", HandleRetryPdfProcessing)
            .RequireSession()
            .WithName("RetryPdfProcessing")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Retry processing of a failed PDF document";
                operation.Description = "Attempts to retry processing of a PDF that failed. Maximum 3 retries allowed. Only the document owner can initiate retry.";
                return operation;
            });
    }

    private static void MapBatchProcessPendingEndpoint(RouteGroupBuilder group)
    {
        // Batch process all pending PDFs (admin-only)
        group.MapPost("/admin/pdfs/process-pending", HandleProcessPendingPdfs)
            .RequireSession()
            .WithName("ProcessPendingPdfs")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Batch process all pending PDFs";
                operation.Description = "Triggers processing for all PDFs stuck in 'pending' or 'processing' status. Admin-only operation for fixing stuck PDF processing pipeline.";
                return operation;
            });
    }

    private static async Task<IResult> HandleGetPdfMetrics(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetPdfMetricsQuery(id);
        var metrics = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(metrics);
    }

    private static async Task<IResult> HandleProcessPendingPdfs(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Validate admin session
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new ProcessPendingPdfsCommand();
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {UserId} triggered batch PDF processing: {Triggered}/{Total} PDFs triggered, {Failed} failed",
            session!.User!.Id,
            result.Triggered,
            result.TotalPending,
            result.Failed);

        return Results.Ok(new
        {
            success = true,
            totalPending = result.TotalPending,
            triggered = result.Triggered,
            failed = result.Failed,
            pdfIds = result.PdfIds,
            message = $"Triggered processing for {result.Triggered} PDFs. Check logs for individual progress."
        });
    }

    private static async Task<IResult> HandleRetryPdfProcessing(
        Guid pdfId,
        IMediator mediator,
        HttpContext context,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Validate session
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        bool isAdmin = string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        var command = new RetryPdfProcessingCommand(
            PdfId: pdfId,
            UserId: session!.User!.Id,
            IsAdmin: isAdmin
        );

        // NotFoundException (404) and ForbiddenException (403) are thrown by the handler
        // and handled by ApiExceptionHandlerMiddleware — no manual mapping needed here.
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            // Domain validation failures: max retries reached → 429, invalid state → 400
            var statusCode = result.Message?.Contains("Maximum retry") == true ? 429 : 400;

            return Results.Json(
                new
                {
                    success = false,
                    message = result.Message,
                    currentState = result.CurrentState,
                    retryCount = result.RetryCount
                },
                statusCode: statusCode
            );
        }

        logger.LogInformation(
            "User {UserId} initiated retry for PDF {PdfId}: {Message}",
            session!.User!.Id,
            pdfId,
            result.Message);

        return Results.Ok(new
        {
            success = true,
            message = result.Message,
            currentState = result.CurrentState,
            retryCount = result.RetryCount
        });
    }

    private static async Task<IResult> HandleGetPdfProcessingProgress(Guid pdfId, HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var pdf = await mediator.Send(new GetPdfProgressQuery(pdfId), ct).ConfigureAwait(false);

        if (pdf == null)
        {
            return Results.NotFound(new { error = "PDF not found" });
        }

        var userId = session!.User!.Id;

        if (pdf.UploadedByUserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }

        ProcessingProgress? progress = null;
        if (!string.IsNullOrEmpty(pdf.ProcessingProgressJson))
        {
            try
            {
                progress = System.Text.Json.JsonSerializer.Deserialize<ProcessingProgress>(pdf.ProcessingProgressJson);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(ex, "Failed to deserialize progress for PDF {PdfId}", pdfId);
            }
#pragma warning restore CA1031
        }

        return Results.Ok(progress);
    }

    private static async Task HandleStreamPdfStatus(Guid pdfId, HttpContext httpContext, IMediator mediator, CancellationToken ct)
    {
        // Issue #4218: Real-time PDF status updates via Server-Sent Events
        var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
        var userId = session.User!.Id;
        bool isAdmin = string.Equals(session.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

        var query = new StreamPdfStatusQuery(pdfId, userId, isAdmin);

        await foreach (var statusEvent in mediator.CreateStream(query, ct).ConfigureAwait(false))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(statusEvent);
            await httpContext.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
            await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Issue #4209: Stream PDF processing progress via SSE.
    /// Supports both public and private PDFs with owner/admin authorization.
    /// Uses CQRS pattern via StreamPdfProgressQuery.
    /// </summary>
    private static async Task HandleStreamPdfProgress(
        Guid pdfId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        // Get authenticated user
        var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
        var userId = session.User!.Id;
        bool isAdmin = string.Equals(session.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

        // Create streaming query (authorization handled in QueryHandler)
        var query = new StreamPdfProgressQuery(pdfId, userId, isAdmin);

        try
        {
            // Stream progress events via MediatR
            await foreach (var progress in mediator.CreateStream(query, ct).ConfigureAwait(false))
            {
                // Serialize progress event
                var json = System.Text.Json.JsonSerializer.Serialize(progress);

                // Send SSE event
                if (progress.Percent == -1 && string.Equals(progress.Message, "heartbeat", StringComparison.Ordinal))
                {
                    // Heartbeat event
                    await httpContext.Response.WriteAsync("event: heartbeat\n", ct).ConfigureAwait(false);
                    await httpContext.Response.WriteAsync($"data: {{\"timestamp\":\"{DateTime.UtcNow:O}\"}}\n\n", ct).ConfigureAwait(false);
                }
                else
                {
                    // Progress event
                    await httpContext.Response.WriteAsync("event: progress\n", ct).ConfigureAwait(false);
                    await httpContext.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                }

                await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - this is expected
        }
    }

    private static async Task<IResult> HandleCancelPdfProcessing(Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;
        bool isAdmin = string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        // DDD Migration Phase 4: Use CancelPdfProcessingCommand via IMediator
        var command = new CancelPdfProcessingCommand(
            PdfId: pdfId,
            UserId: userId,
            IsAdmin: isAdmin
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => Results.NotFound(new { error = result.Message }),
                "FORBIDDEN" => Results.Forbid(),
                "ALREADY_COMPLETED" => Results.BadRequest(new { error = result.Message }),
                "TASK_NOT_FOUND" => Results.BadRequest(new { error = result.Message }),
                _ => Results.BadRequest(new { error = result.Message })
            };
        }

        logger.LogInformation("User {UserId} cancelled processing for PDF {PdfId}", userId, pdfId);

        return Results.Ok(new { message = result.Message });
    }

    private static async Task<IResult> HandleGenerateRuleSpec(Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        if (!string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(session!.User!.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        logger.LogInformation("User {UserId} generating RuleSpec from PDF {PdfId}", session!.User!.Id, pdfId);

        try
        {
            var command = new GenerateRuleSpecFromPdfCommand(pdfId);
            var ruleSpecDto = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Json(ruleSpecDto);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to generate RuleSpec from PDF {PdfId}: {Message}", pdfId, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> HandleIndexPdf(Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        if (!string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(session!.User!.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("User {UserId} with role {Role} attempted to index PDF without permission", session!.User!.Id, session!.User!.Role);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        logger.LogInformation("User {UserId} indexing PDF {PdfId}", session!.User!.Id, pdfId);

        var result = await mediator.Send(new IndexPdfCommand(pdfId.ToString()), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("PDF indexing failed for {PdfId}: {Error}", pdfId, result.ErrorMessage);

            return result.ErrorCode switch
            {
                PdfIndexingErrorCode.PdfNotFound => Results.NotFound(new { error = result.ErrorMessage }),
                PdfIndexingErrorCode.TextExtractionRequired => Results.BadRequest(new { error = result.ErrorMessage }),
                _ => Results.BadRequest(new { error = result.ErrorMessage })
            };
        }

        logger.LogInformation("PDF {PdfId} indexed successfully: {ChunkCount} chunks", pdfId, result.ChunkCount);

        return Results.Json(new
        {
            success = true,
            vectorDocumentId = result.VectorDocumentId,
            chunkCount = result.ChunkCount,
            indexedAt = result.IndexedAt
        });
    }

    private static async Task<IResult> HandleExtractPdfText(Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        if (!string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(session!.User!.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("User {UserId} with role {Role} attempted to extract PDF text without permission", session!.User!.Id, session!.User!.Role);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        logger.LogInformation("User {UserId} extracting text from PDF {PdfId}", session!.User!.Id, pdfId);

        var result = await mediator.Send(new ExtractPdfTextCommand(pdfId), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("PDF text extraction failed for {PdfId}: {Error}", pdfId, result.ErrorMessage);
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation("PDF {PdfId} text extracted successfully: {PageCount} pages, {CharCount} characters",
            pdfId, result.PageCount, result.CharacterCount);

        return Results.Json(new
        {
            success = true,
            characterCount = result.CharacterCount,
            pageCount = result.PageCount,
            processingState = result.ProcessingState
        });
    }
}
