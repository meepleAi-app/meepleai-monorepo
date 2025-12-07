using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Pdf;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfIndexingErrorCode = Api.BoundedContexts.DocumentProcessing.Application.DTOs.PdfIndexingErrorCode;

namespace Api.Routing;

/// <summary>
/// PDF management endpoints.
/// Handles PDF upload, retrieval, deletion, indexing, and rule spec generation.
/// </summary>
public static class PdfEndpoints
{
    public static RouteGroupBuilder MapPdfEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/ingest/pdf", async (HttpContext context, IPdfValidator pdfValidator, IMediator mediator, IFeatureFlagService featureFlags, ILogger<Program> logger, CancellationToken ct) =>
        {
            // CONFIG-05: Check if PDF upload feature is enabled (return 403 before auth to reflect feature gating)
            if (!await featureFlags.IsEnabledAsync("Features.PdfUpload"))
            {
                return Results.Json(
                    new { error = "feature_disabled", message = "PDF uploads are currently disabled", featureName = "Features.PdfUpload" },
                    statusCode: 403);
            }

            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Note: Upload quota enforcement is handled by PdfUploadQuotaService in UploadPdfCommandHandler
            // Admin and Editor roles automatically bypass quota checks
            // Regular users are subject to tier-based daily/weekly limits

            var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);
            var file = form.Files.GetFile("file");
            var gameId = form["gameId"].ToString();

            if (string.IsNullOrWhiteSpace(gameId))
            {
                return Results.BadRequest(new { error = "gameId is required" });
            }

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "No file provided" } });
            }

            var userId = session.User.Id;

            logger.LogInformation("User {UserId} uploading PDF for game {GameId}", userId, gameId);

            // PDF-09: Comprehensive validation (DDD adapter)
            // Validates: magic bytes, file size, MIME type, page count, PDF version
            using var stream = file.OpenReadStream();
            var validation = await pdfValidator.ValidateAsync(stream, file.FileName, ct).ConfigureAwait(false);

            if (!validation.IsValid)
            {
                logger.LogWarning("PDF validation failed for {FileName}: {@Errors}", file.FileName, validation.Errors);
                return Results.BadRequest(new { error = "validation_failed", details = validation.Errors });
            }

            // Reset stream position for processing
            stream.Position = 0;

            var result = await mediator.Send(new UploadPdfCommand(gameId, userId, file!), ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("PDF upload failed for game {GameId}: {Error}", gameId, result.Message);
                return Results.BadRequest(new { error = result.Message });
            }

            if (result.Document == null)
            {
                logger.LogError("PDF upload succeeded but Document is null for game {GameId}", gameId);
                return Results.Problem("Upload succeeded but document is missing", statusCode: 500);
            }

            logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document.Id);
            return Results.Json(new { documentId = result.Document.Id, fileName = result.Document.FileName });
        });

        // Note: Game listing is handled in GameEndpoints to avoid route duplication

        // AI-13: BoardGameGeek API endpoints
        group.MapGet("/bgg/search", async (
            HttpContext context,
            [FromQuery] string? q,
            [FromQuery] bool exact,
            IBggApiService bggService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Validate query parameter
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            var validatedQuery = q!;

            var results = await bggService.SearchGamesAsync(validatedQuery, exact, ct).ConfigureAwait(false);
            logger.LogInformation("BGG search returned {Count} results for query: {Query}", results.Count, validatedQuery);
            return Results.Json(new { results });
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/bgg/games/{bggId:int}", async (
            int bggId,
            HttpContext context,
            IBggApiService bggService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Validate BGG ID
            if (bggId <= 0)
            {
                return Results.BadRequest(new { error = "Invalid BGG ID. Must be a positive integer." });
            }

            var details = await bggService.GetGameDetailsAsync(bggId, ct).ConfigureAwait(false);

            if (details == null)
            {
                logger.LogWarning("BGG game not found: {BggId}", bggId);
                return Results.NotFound(new { error = $"Game with BGG ID {bggId} not found" });
            }

            logger.LogInformation("BGG game details retrieved: {BggId}, {Name}", bggId, details.Name);
            return Results.Json(details);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/games/{gameId:guid}/pdfs", async (Guid gameId, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var pdfs = await mediator.Send(new GetPdfDocumentsByGameQuery(gameId), ct).ConfigureAwait(false);
            return Results.Json(new { pdfs });
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/pdfs/{pdfId:guid}/text", async (Guid pdfId, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            // Use CQRS Query to get PDF text
            var pdf = await mediator.Send(new GetPdfTextQuery(pdfId), ct).ConfigureAwait(false);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            return Results.Json(pdf);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // BGAI-074: Download/view PDF file
        group.MapGet("/pdfs/{pdfId:guid}/download", async (Guid pdfId, HttpContext context, MeepleAiDbContext db, IBlobStorageService blobStorageService, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Get PDF metadata from database
            var pdf = await db.PdfDocuments
                .Where(p => p.Id == pdfId)
                .Select(p => new { p.Id, p.GameId, p.FileName, p.FilePath, p.ContentType, p.UploadedByUserId })
                .FirstOrDefaultAsync(ct);

            if (pdf == null)
            {
                logger.LogWarning("PDF {PdfId} not found for download", pdfId);
                return Results.NotFound(new { error = "PDF not found" });
            }

            var userId = session.User.Id;

            // Authorization: User can only download their own PDFs unless admin
            bool isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isOwner = pdf.UploadedByUserId == userId;

            if (!isAdmin && !isOwner)
            {
                logger.LogWarning("User {UserId} denied access to download PDF {PdfId} (owner: {OwnerId})",
                    session.User.Id, pdfId, pdf.UploadedByUserId);
                return Results.Forbid();
            }

            // Retrieve file from blob storage
            var fileStream = await blobStorageService.RetrieveAsync(pdf.Id.ToString("N"), pdf.GameId.ToString(), ct).ConfigureAwait(false);

            if (fileStream == null)
            {
                logger.LogError("PDF file not found in storage for {PdfId} at path {FilePath}", pdfId, pdf.FilePath);
                return Results.NotFound(new { error = "PDF file not found in storage" });
            }

            logger.LogInformation("User {UserId} downloading PDF {PdfId}", session.User.Id, pdfId);

            // Return file as inline (viewable in browser) with proper content type
            return Results.Stream(fileStream, contentType: pdf.ContentType ?? "application/pdf", fileDownloadName: pdf.FileName, enableRangeProcessing: true);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("DownloadPdf");

        // SEC-02: Delete PDF with Row-Level Security
        group.MapDelete("/pdf/{pdfId:guid}", async (Guid pdfId, HttpContext context, AuditService auditService, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Use CQRS Query to check ownership
            var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct).ConfigureAwait(false);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            var userId = session.User.Id;

            // RLS: Check permissions
            bool isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isOwner = pdf.UploadedByUserId == userId;

            if (!isAdmin && !isOwner)
            {
                // Audit log access denial
                await auditService.LogAsync(
                    session.User.Id.ToString(),
                    "ACCESS_DENIED",
                    "PdfDocument",
                    pdfId.ToString(),
                    "Denied",
                    $"User attempted to delete PDF owned by another user. User role: {session.User.Role}, Owner: {pdf.UploadedByUserId}. RLS scope: own resources only.",
                    null,
                    null,
                    ct);

                logger.LogWarning("User {UserId} with role {Role} denied access to delete PDF {PdfId} (owner: {OwnerId})",
                    session.User.Id, session.User.Role, pdfId, pdf.UploadedByUserId);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Delete PDF
            var result = await mediator.Send(new DeletePdfCommand(pdfId.ToString()), ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogError("Failed to delete PDF {PdfId}: {Error}", pdfId, result.Message);
                return Results.BadRequest(new { error = result.Message });
            }

            logger.LogInformation("User {UserId} deleted PDF {PdfId}", session.User.Id, pdfId);

            // Audit log successful deletion
            await auditService.LogAsync(
                session.User.Id.ToString(),
                "DELETE",
                "PdfDocument",
                pdfId.ToString(),
                "Success",
                $"PDF deleted successfully by user with role: {session.User.Role}",
                null,
                null,
                ct);

            return Results.NoContent();
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // PDF-08: Get PDF processing progress
        group.MapGet("/pdfs/{pdfId:guid}/progress", async (Guid pdfId, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Use CQRS Query to get PDF progress
            var pdf = await mediator.Send(new GetPdfProgressQuery(pdfId), ct).ConfigureAwait(false);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            var userId = session.User.Id;

            // Authorization: User can only view their own PDFs unless admin
            if (pdf.UploadedByUserId != userId &&
                !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Results.Forbid();
            }

            // Deserialize progress from JSON
            ProcessingProgress? progress = null;
            if (!string.IsNullOrEmpty(pdf.ProcessingProgressJson))
            {
                try
                {
                    progress = System.Text.Json.JsonSerializer.Deserialize<ProcessingProgress>(pdf.ProcessingProgressJson);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: Resilience pattern - JSON deserialization failure shouldn't break PDF retrieval
                // Fail-open to return PDF metadata even if progress field is corrupted
                catch (Exception ex)
                {
                    // Resilience pattern: JSON deserialization failure shouldn't break PDF retrieval
                    // Fail-open to return PDF metadata even if progress field is corrupted
                    // Log error but return null progress instead of failing
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning(ex, "Failed to deserialize progress for PDF {PdfId}", pdfId);
                }
#pragma warning restore CA1031
            }

            return Results.Ok(progress);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("GetPdfProcessingProgress");

        // PDF-08: Cancel PDF processing
        group.MapDelete("/pdfs/{pdfId:guid}/processing", async (Guid pdfId, HttpContext context, IMediator mediator, IBackgroundTaskService backgroundTaskService, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Use CQRS Query to check ownership and status
            var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct).ConfigureAwait(false);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            var userId = session.User.Id;

            // Authorization: User can only cancel their own PDFs unless admin
            if (pdf.UploadedByUserId != userId &&
                !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Results.Forbid();
            }

            // Check if processing is active
            if (string.Equals(pdf.ProcessingStatus, "completed", StringComparison.Ordinal) || string.Equals(pdf.ProcessingStatus, "failed", StringComparison.Ordinal))
            {
                return Results.BadRequest(new { error = "Processing already completed or failed" });
            }

            // Cancel the background task
            var cancelled = backgroundTaskService.CancelTask(pdfId.ToString());

            if (!cancelled)
            {
                logger.LogWarning("Failed to cancel processing for PDF {PdfId} - task not found", pdfId);
                return Results.BadRequest(new { error = "Processing task not found or already completed" });
            }

            logger.LogInformation("User {UserId} cancelled processing for PDF {PdfId}", session.User.Id, pdfId);

            return Results.Ok(new { message = "Processing cancellation requested" });
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("CancelPdfProcessing");

        group.MapPost("/ingest/pdf/{pdfId:guid}/rulespec", async (Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            logger.LogInformation("User {UserId} generating RuleSpec from PDF {PdfId}", session.User.Id, pdfId);

            try
            {
                var command = new GenerateRuleSpecFromPdfCommand(pdfId);
                var ruleSpecDto = await mediator.Send(command, ct).ConfigureAwait(false);

                // Issue #1676 Phase 2: Return RuleSpecDto directly (no legacy conversion)
                return Results.Json(ruleSpecDto);
            }
            catch (InvalidOperationException ex)
            {
                // PDF exists but contains no parsable rules - client error
                logger.LogWarning(ex, "Failed to generate RuleSpec from PDF {PdfId}: {Message}", pdfId, ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // AI-01: Index PDF for semantic search
        group.MapPost("/ingest/pdf/{pdfId:guid}/index", async (Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} with role {Role} attempted to index PDF without permission", session.User.Id, session.User.Role);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            logger.LogInformation("User {UserId} indexing PDF {PdfId}", session.User.Id, pdfId);

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
        });

        // BGAI-081: Extract text from existing PDF (reprocess stuck PDFs)
        group.MapPost("/ingest/pdf/{pdfId:guid}/extract", async (Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} with role {Role} attempted to extract PDF text without permission", session.User.Id, session.User.Role);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            logger.LogInformation("User {UserId} extracting text from PDF {PdfId}", session.User.Id, pdfId);

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
                processingStatus = result.ProcessingStatus
            });
        });

        // ============================================
        // Chunked Upload Endpoints (for large PDFs > 30 MB)
        // ============================================

        // Initialize chunked upload session
        group.MapPost("/ingest/pdf/chunked/init", async (
            HttpContext context,
            [FromBody] InitChunkedUploadRequest request,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Check if PDF upload feature is enabled
            if (!await featureFlags.IsEnabledAsync("Features.PdfUpload"))
            {
                return Results.Json(
                    new { error = "feature_disabled", message = "PDF uploads are currently disabled" },
                    statusCode: 403);
            }

            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = session.User.Id;

            logger.LogInformation(
                "User {UserId} initializing chunked upload for game {GameId}, file {FileName} ({FileSize} bytes)",
                userId, request.GameId, request.FileName, request.TotalFileSize);

            var command = new InitChunkedUploadCommand(
                request.GameId,
                userId,
                request.FileName,
                request.TotalFileSize);

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
        })
        .RequireSession();

        // Upload a single chunk
        group.MapPost("/ingest/pdf/chunked/chunk", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = session.User.Id;

            var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);

            if (!Guid.TryParse(form["sessionId"].ToString(), out var sessionId))
            {
                return Results.BadRequest(new { error = "invalid_session_id", message = "Invalid or missing session ID" });
            }

            if (!int.TryParse(form["chunkIndex"].ToString(), out var chunkIndex))
            {
                return Results.BadRequest(new { error = "invalid_chunk_index", message = "Invalid or missing chunk index" });
            }

            var file = form.Files.GetFile("chunk");
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "missing_chunk", message = "No chunk data provided" });
            }

            // Read chunk data
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, ct).ConfigureAwait(false);
            var chunkData = memoryStream.ToArray();

            logger.LogDebug("Received chunk {ChunkIndex} for session {SessionId} ({Size} bytes)",
                chunkIndex, sessionId, chunkData.Length);

            var command = new UploadChunkCommand(sessionId, userId, chunkIndex, chunkData);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("Failed to upload chunk {ChunkIndex} for session {SessionId}: {Error}",
                    chunkIndex, sessionId, result.ErrorMessage);
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
        })
        .RequireSession();

        // Complete chunked upload and trigger processing
        group.MapPost("/ingest/pdf/chunked/complete", async (
            HttpContext context,
            [FromBody] CompleteChunkedUploadRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = session.User.Id;

            logger.LogInformation("User {UserId} completing chunked upload session {SessionId}",
                userId, request.SessionId);

            var command = new CompleteChunkedUploadCommand(request.SessionId, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("Failed to complete chunked upload {SessionId}: {Error}",
                    request.SessionId, result.ErrorMessage);

                if (result.MissingChunks != null && result.MissingChunks.Count > 0)
                {
                    return Results.BadRequest(new
                    {
                        error = result.ErrorMessage,
                        missingChunks = result.MissingChunks
                    });
                }

                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            logger.LogInformation("Chunked upload completed: Document {DocumentId}", result.DocumentId);

            return Results.Json(new
            {
                success = true,
                documentId = result.DocumentId,
                fileName = result.FileName
            });
        })
        .RequireSession();

        // Get chunked upload session status
        group.MapGet("/ingest/pdf/chunked/{sessionId:guid}/status", async (
            Guid sessionId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = session.User.Id;

            var query = new GetChunkedUploadStatusQuery(sessionId, userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = "Session not found or access denied" });
            }

            return Results.Json(result);
        })
        .RequireSession();

        return group;
    }
}

/// <summary>
/// Request model for initializing a chunked upload session.
/// </summary>
public record InitChunkedUploadRequest(
    Guid GameId,
    string FileName,
    long TotalFileSize
);

/// <summary>
/// Request model for completing a chunked upload.
/// </summary>
public record CompleteChunkedUploadRequest(
    Guid SessionId
);