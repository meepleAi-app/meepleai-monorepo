using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PdfIndexingErrorCode = Api.BoundedContexts.DocumentProcessing.Application.DTOs.PdfIndexingErrorCode;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// PDF management endpoints.
/// Handles PDF upload, retrieval, deletion, indexing, and rule spec generation.
/// </summary>
internal static class PdfEndpoints
{
    public static RouteGroupBuilder MapPdfEndpoints(this RouteGroupBuilder group)
    {
        MapStandardUploadEndpoint(group);
        MapPrivatePdfUploadEndpoint(group); // Issue #3479: Private PDF Upload
        MapChunkedUploadEndpoints(group);
        // Removed: MapBggEndpoints(group) - BGG belongs to AiEndpoints (Issue #2366)
        MapRetrievalEndpoints(group);
        MapLifecycleEndpoints(group);
        MapProcessingStateEndpoints(group);
        MapProcessingActionsEndpoints(group);
        MapBggExtractionEndpoint(group); // ISSUE-2513: BGG games extraction from PDF
        MapAdminPdfListEndpoint(group); // Admin: List all PDFs with status

        return group;
    }

    private static void MapStandardUploadEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/ingest/pdf", HandleStandardUpload)
             .DisableAntiforgery() // Ensure we can post files
             .WithMetadata(new RequestSizeLimitAttribute(104_857_600)); // 100 MB for large rulebooks
    }

    /// <summary>
    /// Issue #3479: Private PDF upload endpoint for UserLibraryEntry.
    /// POST /users/{userId}/library/entries/{entryId}/pdf
    /// </summary>
    private static void MapPrivatePdfUploadEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/users/{userId:guid}/library/entries/{entryId:guid}/pdf", HandlePrivatePdfUpload)
             .DisableAntiforgery()
             .RequireSession()
             .WithName("UploadPrivatePdf")
             .WithOpenApi(operation =>
             {
                 operation.Summary = "Upload a private PDF for a library entry";
                 operation.Description = "Uploads a private PDF file and associates it with a user's library entry. The PDF will be processed and indexed in a private namespace.";
                 return operation;
             });
    }

    private static void MapChunkedUploadEndpoints(RouteGroupBuilder group)
    {
        MapChunkedUploadInitEndpoint(group);
        MapChunkedUploadCompleteEndpoint(group);
        MapChunkedUploadStatusEndpoint(group);
        MapChunkedUploadTransferEndpoints(group);
    }

    private static void MapChunkedUploadInitEndpoint(RouteGroupBuilder group)
    {
        // Initialize chunked upload session
        group.MapPost("/ingest/pdf/chunked/init", HandleInitChunkedUpload)
        .RequireSession();
    }

    private static void MapChunkedUploadCompleteEndpoint(RouteGroupBuilder group)
    {
        // Complete chunked upload and trigger processing
        group.MapPost("/ingest/pdf/chunked/complete", HandleCompleteChunkedUpload)
        .RequireSession();
    }

    private static void MapChunkedUploadStatusEndpoint(RouteGroupBuilder group)
    {
        // Get chunked upload session status
        group.MapGet("/ingest/pdf/chunked/{sessionId:guid}/status", HandleGetChunkedUploadStatus)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapChunkedUploadTransferEndpoints(RouteGroupBuilder group)
    {
        // Upload a single chunk
        group.MapPost("/ingest/pdf/chunked/chunk", HandleUploadChunk)
        .RequireSession();
    }

    private static void MapRetrievalEndpoints(RouteGroupBuilder group)
    {
        MapPdfListEndpoint(group);
        MapPdfTextEndpoint(group);
        MapPdfDownloadEndpoint(group);
    }

    private static void MapPdfListEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/games/{gameId:guid}/pdfs", HandleListPdfs)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapPdfTextEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/pdfs/{pdfId:guid}/text", HandleGetPdfText)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapPdfDownloadEndpoint(RouteGroupBuilder group)
    {
        // BGAI-074: Download/view PDF file
        group.MapGet("/pdfs/{pdfId:guid}/download", HandleDownloadPdf)
        .RequireSession()
        .RequireAuthorization()
        .WithName("DownloadPdf");
    }

    private static void MapLifecycleEndpoints(RouteGroupBuilder group)
    {
        MapPdfDeletionEndpoints(group);
        MapPdfVisibilityEndpoints(group);
        MapPdfLanguageEndpoints(group);
    }

    private static void MapPdfLanguageEndpoints(RouteGroupBuilder group)
    {
        // E5-2: Override detected language of a PDF document
        group.MapPut("/pdfs/{pdfId:guid}/language", HandleOverridePdfLanguage)
        .RequireSession()
        .RequireAuthorization()
        .WithName("OverridePdfLanguage");
    }

    private static void MapPdfDeletionEndpoints(RouteGroupBuilder group)
    {
        // SEC-02: Delete PDF with Row-Level Security
        group.MapDelete("/pdf/{pdfId:guid}", HandleDeletePdf)
        .RequireSession();
    }

    private static void MapPdfVisibilityEndpoints(RouteGroupBuilder group)
    {
        // Admin Wizard: Set PDF visibility in public library
        group.MapPatch("/pdfs/{pdfId:guid}/visibility", HandleSetPdfVisibility)
        .RequireSession()
        .RequireAuthorization()
        .WithName("SetPdfVisibility");

        // Issue #5446: Accept copyright disclaimer for a PDF
        group.MapPost("/documents/{pdfId:guid}/accept-disclaimer", HandleAcceptCopyrightDisclaimer)
        .RequireSession()
        .RequireAuthorization()
        .WithName("AcceptCopyrightDisclaimer");

        // Issue #5446: Toggle RAG active flag for a PDF
        group.MapPatch("/documents/{pdfId:guid}/active", HandleSetActiveForRag)
        .RequireSession()
        .RequireAuthorization()
        .WithName("SetActiveForRag");

        // Issue #5447: Reclassify document (category, base document, version label)
        group.MapPatch("/documents/{pdfId:guid}/classify", HandleReclassifyDocument)
        .RequireSession()
        .RequireAuthorization()
        .WithName("ReclassifyDocument");
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

    private static async Task<IResult> HandleGetPdfMetrics(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetPdfMetricsQuery(id);
        var metrics = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(metrics);
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

    private static async Task<IResult> HandleStandardUpload(
        HttpContext context,
                IMediator mediator,
        IFeatureFlagService featureFlags,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // CONFIG-05: Check if PDF upload feature is enabled
        if (!await featureFlags.IsEnabledAsync("Features.PdfUpload").ConfigureAwait(false))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "PDF uploads are currently disabled", featureName = "Features.PdfUpload" },
                statusCode: 403);
        }

        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);
        var file = form.Files.GetFile("file");

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "No file provided" } });
        }

        // Private game upload: frontend sends 'privateGameId' field to route through
        // HandlePrivateGamePdfUploadAsync (ownership validation, correct DB link).
        Guid? privateGameId = null;
        var privateGameIdStr = form["privateGameId"].ToString();
        if (!string.IsNullOrWhiteSpace(privateGameIdStr) && Guid.TryParse(privateGameIdStr, out var parsedPrivateGameId))
        {
            privateGameId = parsedPrivateGameId;
        }

        string? gameId = null;
        PdfUploadMetadata? metadata = null;
        if (!privateGameId.HasValue)
        {
            var (parsedGameId, parsedMetadata, validationError) = ParseUploadMetadata(form);
            if (validationError != null)
            {
                return Results.BadRequest(new { error = validationError });
            }
            gameId = parsedGameId;
            metadata = parsedMetadata;
        }

        var userId = session!.User!.Id;
        var priority = context.Request.Query["priority"].FirstOrDefault();
        // Bug fix: Only allow priority override for admin users to prevent privilege escalation
        if (priority != null && !string.Equals(session.User.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            priority = null;
        }
        var result = await mediator.Send(new UploadPdfCommand(gameId, metadata, privateGameId, userId, file!, Priority: priority), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("PDF upload failed: {Error}", result.Message);
            // Return 409 Conflict for duplicate content uploads
            if (string.Equals(result.Message, UploadPdfCommandHandler.DuplicateContentErrorMessage, StringComparison.Ordinal))
            {
                return Results.Conflict(new { error = "duplicate_content", message = result.Message });
            }
            return Results.BadRequest(new { error = result.Message });
        }

        if (result.Document == null)
        {
            logger.LogError("PDF upload succeeded but Document is null");
            return Results.Problem("Upload succeeded but document is missing", statusCode: 500);
        }

        logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document.Id);
        return Results.Json(new { documentId = result.Document.Id, fileName = result.Document.FileName });
    }

    private static async Task<IResult> HandleInitChunkedUpload(
        HttpContext context,
        [FromBody] InitChunkedUploadRequest request,
        IMediator mediator,
        IFeatureFlagService featureFlags,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!await featureFlags.IsEnabledAsync("Features.PdfUpload").ConfigureAwait(false))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "PDF uploads are currently disabled" },
                statusCode: 403);
        }

        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation(
            "User {UserId} initializing chunked upload for game {GameId}, file {FileName} ({FileSize} bytes)",
            userId, request.GameId, request.FileName, request.TotalFileSize);

        var command = new InitChunkedUploadCommand(request.GameId, userId, request.FileName, request.TotalFileSize, request.PrivateGameId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Failed to initialize chunked upload: {Error}", result.ErrorMessage);
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Json(new
        {
            sessionId = result.SessionId,
            totalChunks = result.TotalChunks,
            chunkSizeBytes = result.ChunkSizeBytes,
            expiresAt = result.ExpiresAt
        });
    }

    private static async Task<IResult> HandleCompleteChunkedUpload(
        HttpContext context,
        [FromBody] CompleteChunkedUploadRequest request,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation("User {UserId} completing chunked upload session {SessionId}", userId, request.SessionId);

        var command = new CompleteChunkedUploadCommand(request.SessionId, userId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Failed to complete chunked upload {SessionId}: {Error}", request.SessionId, result.ErrorMessage);

            // Return 409 Conflict for duplicate content uploads
            if (string.Equals(result.ErrorMessage, CompleteChunkedUploadCommandHandler.DuplicateContentErrorMessage, StringComparison.Ordinal))
            {
                return Results.Conflict(new { error = "duplicate_content", message = result.ErrorMessage });
            }

            if (result.MissingChunks != null && result.MissingChunks.Count > 0)
            {
                return Results.BadRequest(new { error = result.ErrorMessage, missingChunks = result.MissingChunks });
            }

            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation("Chunked upload completed: Document {DocumentId}", result.DocumentId);

        return Results.Json(new { success = true, documentId = result.DocumentId, fileName = result.FileName });
    }

    private static async Task<IResult> HandleGetChunkedUploadStatus(
        Guid sessionId,
        HttpContext context,
        IMediator mediator,
                CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        var userId = session!.User!.Id;

        var query = new GetChunkedUploadStatusQuery(sessionId, userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        if (result == null)
        {
            return Results.NotFound(new { error = "Session not found or access denied" });
        }

        return Results.Json(result);
    }

    private static async Task<IResult> HandleUploadChunk(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        var userId = session!.User!.Id;
        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);

        if (!Guid.TryParse(form["sessionId"].ToString(), out var sessionId))
        {
            return Results.BadRequest(new { error = "invalid_session_id", message = "Invalid or missing session ID" });
        }

        if (!int.TryParse(form["chunkIndex"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var chunkIndex))
        {
            return Results.BadRequest(new { error = "invalid_chunk_index", message = "Invalid or missing chunk index" });
        }

        var file = form.Files.GetFile("chunk");
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "missing_chunk", message = "No chunk data provided" });
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, ct).ConfigureAwait(false);
        var chunkData = memoryStream.ToArray();

        logger.LogDebug("Received chunk {ChunkIndex} for session {SessionId} ({Size} bytes)", chunkIndex, sessionId, chunkData.Length);

        var command = new UploadChunkCommand(sessionId, userId, chunkIndex, chunkData);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Failed to upload chunk {ChunkIndex} for session {SessionId}: {Error}", chunkIndex, sessionId, result.ErrorMessage);
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Json(new
        {
            success = true,
            receivedChunks = result.ReceivedChunks,
            totalChunks = result.TotalChunks,
            progressPercentage = result.ProgressPercentage,
            isComplete = result.IsComplete
        });
    }

    private static async Task<IResult> HandleListPdfs(Guid gameId, IMediator mediator, CancellationToken ct)
    {
        var pdfs = await mediator.Send(new GetPdfDocumentsByGameQuery(gameId), ct).ConfigureAwait(false);
        return Results.Json(new { pdfs });
    }

    private static async Task<IResult> HandleGetPdfText(Guid pdfId, IMediator mediator, CancellationToken ct)
    {
        var pdf = await mediator.Send(new GetPdfTextQuery(pdfId), ct).ConfigureAwait(false);

        if (pdf == null)
        {
            return Results.NotFound(new { error = "PDF not found" });
        }

        return Results.Json(pdf);
    }

    private static async Task<IResult> HandleDownloadPdf(Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;
        bool isAdmin = string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        // DDD Migration Phase 4: Use DownloadPdfQuery via IMediator
        var query = new DownloadPdfQuery(
            PdfId: pdfId,
            UserId: userId,
            IsAdmin: isAdmin
        );

        PdfDownloadResult? result;
        try
        {
            result = await mediator.Send(query, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied for user {UserId} to download PDF {PdfId}", userId, pdfId);
            return Results.Forbid();
        }

        if (result == null)
        {
            logger.LogWarning("PDF {PdfId} not found for download", pdfId);
            return Results.NotFound(new { error = "PDF not found or file not in storage" });
        }

        logger.LogInformation("User {UserId} downloading PDF {PdfId}", userId, pdfId);

        return Results.Stream(
            result.FileStream,
            contentType: result.ContentType,
            fileDownloadName: result.FileName,
            enableRangeProcessing: true);
    }

    private static async Task<IResult> HandleDeletePdf(
        Guid pdfId,
        HttpContext context,
        AuditService auditService,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct).ConfigureAwait(false);

        if (pdf == null)
        {
            return Results.NotFound(new { error = "PDF not found" });
        }

        if (!CheckPdfAuthorization(session.User!, pdf))
        {
            await LogPdfAccessDeniedAsync(auditService, session.User!.Id.ToString(), "DELETE", pdfId.ToString(), session.User!.Role ?? "Unknown", pdf.UploadedByUserId, ct).ConfigureAwait(false);

            logger.LogWarning("User {UserId} with role {Role} denied access to delete PDF {PdfId} (owner: {OwnerId})",
                session.User!.Id, session.User!.Role, pdfId, pdf.UploadedByUserId);

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await mediator.Send(new DeletePdfCommand(pdfId.ToString()), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogError("Failed to delete PDF {PdfId}: {Error}", pdfId, result.Message);
            return Results.BadRequest(new { error = result.Message });
        }

        logger.LogInformation("User {UserId} deleted PDF {PdfId}", session.User!.Id, pdfId);

        await auditService.LogAsync(
            session.User!.Id.ToString(),
            "DELETE",
            "PdfDocument",
            pdfId.ToString(),
            "Success",
            $"PDF deleted successfully by user with role: {session.User!.Role}",
            null,
            null,
            ct).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleSetPdfVisibility(
        Guid pdfId,
        HttpContext context,
        [FromBody] SetPdfVisibilityRequest request,
        IMediator mediator,
        AuditService auditService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct).ConfigureAwait(false);

        if (pdf == null)
        {
            return Results.NotFound(new { error = "PDF not found" });
        }

        if (!CheckPdfAuthorization(session.User!, pdf))
        {
            await LogPdfAccessDeniedAsync(auditService, userId.ToString(), "change visibility of", pdfId.ToString(), session.User!.Role ?? "Unknown", pdf.UploadedByUserId, ct).ConfigureAwait(false);

            logger.LogWarning("User {UserId} denied access to change visibility of PDF {PdfId} (owner: {OwnerId})",
                userId, pdfId, pdf.UploadedByUserId);

            return Results.Forbid();
        }

        var result = await mediator.Send(new SetPdfVisibilityCommand(pdfId, request.IsPublic), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Failed to set visibility for PDF {PdfId}: {Error}", pdfId, result.Message);
            return Results.BadRequest(new { error = result.Message });
        }

        logger.LogInformation("User {UserId} set PDF {PdfId} visibility to {IsPublic}", userId, pdfId, request.IsPublic);

        await auditService.LogAsync(
            userId.ToString(),
            "UPDATE_VISIBILITY",
            "PdfDocument",
            pdfId.ToString(),
            "Success",
            $"PDF visibility changed to {(request.IsPublic ? "public" : "private")} by user with role: {session!.User!.Role}",
            null,
            null,
            ct).ConfigureAwait(false);

        return Results.Ok(new { success = true, message = result.Message, isPublic = request.IsPublic });
    }

    /// <summary>
    /// Issue #5446: Accept copyright disclaimer for a PDF document.
    /// </summary>
    private static async Task<IResult> HandleAcceptCopyrightDisclaimer(
        Guid pdfId,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        var result = await mediator.Send(new AcceptCopyrightDisclaimerCommand(userId, pdfId), ct).ConfigureAwait(false);

        return Results.Ok(new { success = result.Success, message = result.Message });
    }

    /// <summary>
    /// Issue #5446: Toggle RAG active flag for a PDF document.
    /// </summary>
    private static async Task<IResult> HandleSetActiveForRag(
        Guid pdfId,
        [FromBody] SetActiveForRagRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SetActiveForRagCommand(pdfId, request.IsActive), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Message });
        }

        return Results.Ok(new { success = true, message = result.Message, isActive = request.IsActive });
    }

    private static async Task<IResult> HandleReclassifyDocument(
        Guid pdfId,
        [FromBody] ReclassifyDocumentRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ReclassifyDocumentCommand(pdfId, request.Category, request.BaseDocumentId, request.VersionLabel), ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Message });
        }

        return Results.Ok(new { success = true, message = result.Message, pdfId = result.PdfId });
    }

    // Helpers
    private static (string gameId, PdfUploadMetadata? metadata, string? error) ParseUploadMetadata(IFormCollection form)
    {
        var gameId = form["gameId"].ToString();
        var gameName = form["gameName"].ToString();
        var versionType = form["versionType"].ToString();
        var language = form["language"].ToString();
        var versionNumber = form["versionNumber"].ToString();

        bool hasGameId = !string.IsNullOrWhiteSpace(gameId);
        bool hasMetadata = !string.IsNullOrWhiteSpace(gameName);

        if (!hasGameId && !hasMetadata)
        {
            return (gameId, null, "Either gameId or game metadata (gameName, versionType, language, versionNumber) is required");
        }

        PdfUploadMetadata? metadata = null;
        if (hasMetadata)
        {
            metadata = new PdfUploadMetadata(
                gameName,
                versionType ?? "base",
                language ?? "en",
                versionNumber ?? "1.0"
            );
        }

        return (gameId, metadata, null);
    }

    private static bool CheckPdfAuthorization(Api.BoundedContexts.Authentication.Application.DTOs.UserDto user, PdfOwnershipResult pdf)
    {
        bool isAdmin = string.Equals(user.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
        bool isOwner = pdf.UploadedByUserId == user.Id;
        return isAdmin || isOwner;
    }

    private static Task LogPdfAccessDeniedAsync(AuditService auditService, string userId, string action, string pdfId, string role, Guid ownerId, CancellationToken ct)
    {
        return auditService.LogAsync(
                userId,
                "ACCESS_DENIED",
                "PdfDocument",
                pdfId,
                "Denied",
                $"User attempted to {action} PDF owned by another user. User role: {role}, Owner: {ownerId}. RLS scope: own resources only.",
                null,
                null,
                ct);
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

    /// <summary>
    /// Issue #3479: Handle private PDF upload for UserLibraryEntry.
    /// </summary>
    private static async Task<IResult> HandlePrivatePdfUpload(
        Guid userId,
        Guid entryId,
        HttpContext context,
        IMediator mediator,
        IFeatureFlagService featureFlags,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Check feature flag
        if (!await featureFlags.IsEnabledAsync("Features.PdfUpload").ConfigureAwait(false))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "PDF uploads are currently disabled" },
                statusCode: 403);
        }

        // Validate session
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        // Verify user matches session
        if (session!.User!.Id != userId)
        {
            logger.LogWarning(
                "User {SessionUserId} attempted to upload PDF as user {RequestedUserId}",
                session.User.Id, userId);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        // Read file from form
        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);
        var file = form.Files.GetFile("file");

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new
            {
                error = "validation_failed",
                message = "No file provided. Please select a PDF file to upload."
            });
        }

        // Create and send command
        var command = new UploadPrivatePdfCommand(userId, entryId, file);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Private PDF {PdfId} uploaded for library entry {EntryId} by user {UserId}",
                result.PdfId, entryId, userId);

            return Results.Created(
                $"/api/v1/pdfs/{result.PdfId}",
                new
                {
                    pdfId = result.PdfId,
                    fileName = result.FileName,
                    fileSize = result.FileSize,
                    status = result.Status,
                    sseStreamUrl = result.SseStreamUrl
                });
        }
        catch (Api.Middleware.Exceptions.NotFoundException ex)
        {
            logger.LogWarning(ex, "Library entry not found for private PDF upload: {Message}", ex.Message);
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Api.Middleware.Exceptions.ForbiddenException ex)
        {
            logger.LogWarning(ex, "Forbidden access for private PDF upload: {Message}", ex.Message);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (Api.SharedKernel.Domain.Exceptions.ValidationException ex)
        {
            logger.LogWarning(ex, "Validation failed for private PDF upload: {Message}", ex.Message);
            return Results.BadRequest(new { error = "validation_failed", message = ex.Message });
        }
    }

    /// <summary>
    /// ISSUE-2513: Extract BGG games from PDF for SharedGames seeding
    /// </summary>
    private static void MapBggExtractionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/extract-bgg-games", async (
            [FromBody] ExtractBggGamesRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new ExtractBggGamesFromPdfQuery(request.PdfFilePath);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                success = true,
                gameCount = result.Count,
                games = result
            });
        })
        .RequireAuthorization()
        .WithName("ExtractBggGamesFromPdf")
        .WithTags("DocumentProcessing", "BGG")
        .WithSummary("Extract BGG games list from PDF")
        .WithDescription("Parses PDF document to extract structured game data (Name, BGG ID) for SharedGames seeding")
        .Produces<object>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapAdminPdfListEndpoint(RouteGroupBuilder group)
    {
        // Admin: List all PDFs with processing status
        group.MapGet("/admin/pdfs", HandleGetAllPdfs)
            .RequireSession()
            .WithName("GetAllPdfs")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get all PDF documents (admin-only)";
                operation.Description = "Returns list of all PDFs with processing status, game association, and chunk counts.";
                return operation;
            });
    }

    private static async Task<IResult> HandleGetAllPdfs(
        HttpContext context,
        IMediator mediator,
        [FromQuery] string? status,
        [FromQuery] string? state,
        [FromQuery] long? minSizeBytes,
        [FromQuery] long? maxSizeBytes,
        [FromQuery] DateTime? uploadedAfter,
        [FromQuery] DateTime? uploadedBefore,
        [FromQuery] Guid? gameId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] int pageSize = 50,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllPdfsQuery(
            Status: status,
            State: state,
            MinSizeBytes: minSizeBytes,
            MaxSizeBytes: maxSizeBytes,
            UploadedAfter: uploadedAfter,
            UploadedBefore: uploadedBefore,
            GameId: gameId,
            SortBy: sortBy,
            SortOrder: sortOrder,
            PageSize: pageSize,
            Page: page
        );
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// E5-2: Override detected language of a PDF document.
    /// </summary>
    private static async Task<IResult> HandleOverridePdfLanguage(
        Guid pdfId,
        [FromBody] OverridePdfLanguageRequest request,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        await mediator.Send(new OverridePdfLanguageCommand(pdfId, request.LanguageCode), ct).ConfigureAwait(false);

        logger.LogInformation("PDF {PdfId} language override set to {LanguageCode}", pdfId, request.LanguageCode ?? "(cleared)");

        return Results.Ok(new { success = true, languageCode = request.LanguageCode });
    }
}

internal record InitChunkedUploadRequest(Guid? GameId, string FileName, long TotalFileSize, Guid? PrivateGameId = null);
internal record CompleteChunkedUploadRequest(Guid SessionId);
internal record SetPdfVisibilityRequest(bool IsPublic);
internal record SetActiveForRagRequest(bool IsActive); // Issue #5446
internal record ReclassifyDocumentRequest(string Category, Guid? BaseDocumentId, string? VersionLabel); // Issue #5447
internal record ExtractBggGamesRequest(string PdfFilePath); // ISSUE-2513
internal record OverridePdfLanguageRequest(string? LanguageCode); // E5-2