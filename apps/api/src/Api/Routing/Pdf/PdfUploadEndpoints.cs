using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// PDF upload endpoints: standard upload, private upload, chunked upload, and BGG extraction.
/// </summary>
internal static class PdfUploadEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapStandardUploadEndpoint(group);
        MapPrivatePdfUploadEndpoint(group); // Issue #3479: Private PDF Upload
        MapChunkedUploadEndpoints(group);
        MapBggExtractionEndpoint(group); // ISSUE-2513: BGG games extraction from PDF
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

        if (result.ExistingKb != null)
        {
            return Results.Ok(new
            {
                existingKbFound = true,
                existingKb = result.ExistingKb,
                message = result.Message
            });
        }

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
}
