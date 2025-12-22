using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
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
        MapChunkedUploadEndpoints(group);
        MapBggEndpoints(group);
        MapRetrievalEndpoints(group);
        MapLifecycleEndpoints(group);
        MapProcessingStateEndpoints(group);
        MapProcessingActionsEndpoints(group);

        return group;
    }

    private static void MapStandardUploadEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/ingest/pdf", HandleStandardUpload)
             .DisableAntiforgery(); // Ensure we can post files
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
        .RequireSession();
    }

    private static void MapChunkedUploadTransferEndpoints(RouteGroupBuilder group)
    {
        // Upload a single chunk
        group.MapPost("/ingest/pdf/chunked/chunk", HandleUploadChunk)
        .RequireSession();
    }

    private static void MapBggEndpoints(RouteGroupBuilder group)
    {
        // Note: Game listing is handled in GameEndpoints to avoid route duplication

        // AI-13: BoardGameGeek API endpoints
        group.MapGet("/bgg/search", HandleBggSearch)
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/bgg/games/{bggId:int}", HandleGetBggGameDetails)
        .RequireSession(); // Issue #1446: Automatic session validation
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
    }

    private static void MapProcessingStateEndpoints(RouteGroupBuilder group)
    {
        MapProcessingProgressEndpoint(group);
        MapProcessingCancelEndpoint(group);
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

        var (gameId, metadata, validationError) = ParseUploadMetadata(form);
        if (validationError != null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        var userId = session!.User!.Id;
        var result = await mediator.Send(new UploadPdfCommand(gameId, metadata, userId, file!), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("PDF upload failed: {Error}", result.Message);
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

        var command = new InitChunkedUploadCommand(request.GameId, userId, request.FileName, request.TotalFileSize);
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

    private static async Task<IResult> HandleBggSearch(
                [FromQuery] string? q,
        [FromQuery] bool exact,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest(new { error = "Query parameter 'q' is required" });
        }

        var validatedQuery = q!;

        // DDD Migration Phase 3.3: Use SearchBggGamesQuery via IMediator
        var query = new SearchBggGamesQuery(
            SearchTerm: validatedQuery,
            ExactMatch: exact
        );

        var results = await mediator.Send(query, ct).ConfigureAwait(false);
        logger.LogInformation("BGG search returned {Count} results for query: {Query}", results.Count, validatedQuery);
        return Results.Json(new { results });
    }

    private static async Task<IResult> HandleGetBggGameDetails(
        int bggId,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (bggId <= 0)
        {
            return Results.BadRequest(new { error = "Invalid BGG ID. Must be a positive integer." });
        }

        // DDD Migration Phase 3.3: Use GetBggGameDetailsQuery via IMediator
        var query = new GetBggGameDetailsQuery(BggId: bggId);
        var details = await mediator.Send(query, ct).ConfigureAwait(false);

        if (details == null)
        {
            logger.LogWarning("BGG game not found: {BggId}", bggId);
            return Results.NotFound(new { error = $"Game with BGG ID {bggId} not found" });
        }

        logger.LogInformation("BGG game details retrieved: {BggId}, {Name}", bggId, details.Name);
        return Results.Json(details);
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
            processingStatus = result.ProcessingStatus
        });
    }
}

internal record InitChunkedUploadRequest(Guid GameId, string FileName, long TotalFileSize);
internal record CompleteChunkedUploadRequest(Guid SessionId);
internal record SetPdfVisibilityRequest(bool IsPublic);

