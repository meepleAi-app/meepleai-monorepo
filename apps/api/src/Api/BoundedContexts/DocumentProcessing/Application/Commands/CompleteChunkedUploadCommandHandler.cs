using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Models;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for CompleteChunkedUploadCommand.
/// Assembles chunks into a complete file, stores it and hands the document to the processing queue.
/// </summary>
internal class CompleteChunkedUploadCommandHandler : ICommandHandler<CompleteChunkedUploadCommand, CompleteChunkedUploadResult>
{
    private static readonly string UploadTempBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");

    /// <summary>Same value as the admin upload panel's «Normale» (<c>PRIORITY_NORMAL</c> in queue-api.ts).</summary>
    internal const int InteractiveUploadPriority = 10;

    private readonly IChunkedUploadSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly ILogger<CompleteChunkedUploadCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMediator _mediator;
    private readonly IPdfDeduplicationService _pdfDeduplicationService;

    public CompleteChunkedUploadCommandHandler(
        IChunkedUploadSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        IBlobStorageService blobStorageService,
        IBackgroundTaskService backgroundTaskService,
        ILogger<CompleteChunkedUploadCommandHandler> logger,
        IMediator mediator,
        IPdfDeduplicationService pdfDeduplicationService,
        TimeProvider? timeProvider = null)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _pdfDeduplicationService = pdfDeduplicationService ?? throw new ArgumentNullException(nameof(pdfDeduplicationService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CompleteChunkedUploadResult> Handle(
        CompleteChunkedUploadCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            // Validate session and permissions
            var (validationSuccess, validationError, session) = await ValidateSessionAndPermissionsAsync(
                request.SessionId, request.UserId, cancellationToken).ConfigureAwait(false);
            if (!validationSuccess)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session?.FileName,
                    ErrorMessage: validationError,
                    MissingChunks: session != null && !session.IsComplete ? session.GetMissingChunks() : null
                );
            }

            _logger.LogInformation(
                "Assembling chunked upload {SessionId} ({TotalChunks} chunks, {TotalSize} bytes)",
                request.SessionId, session!.TotalChunks, session.TotalFileSize);

            // Assemble chunks and store in blob storage
            (bool storageSuccess, BlobStorageResult? storageResult, string? sanitizedFileName, string? contentHash) = await AssembleAndStoreFileAsync(
                session, cancellationToken).ConfigureAwait(false);
            if (!storageSuccess)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: storageResult!.ErrorMessage ?? "Failed to store assembled file",
                    MissingChunks: null
                );
            }

            // Check for duplicate content and, if found, transparently reuse the
            // existing PDF document instead of rejecting the upload (Task 2 of the
            // PDF dedup alignment plan; centralizes the rule previously duplicated
            // and divergent between AddRulebookCommandHandler (reuse) and this
            // handler (reject) in IPdfDeduplicationService).
            if (contentHash != null)
            {
                var dedup = await _pdfDeduplicationService
                    .EvaluateAsync(contentHash, session.GameId, session.PrivateGameId, session.UserId, cancellationToken)
                    .ConfigureAwait(false);

                if (dedup.Decision == PdfDedupDecision.ReuseExisting)
                {
                    // Cleanup del file appena assemblato (best effort): non serve, riusiamo l'esistente.
                    if (storageResult!.FileId != null)
                    {
                        try
                        {
                            await _blobStorageService.DeleteAsync(storageResult.FileId, BlobCategory.Pdf,
                                (session.PrivateGameId ?? session.GameId)?.ToString() ?? string.Empty, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch { /* best effort */ }
                    }

                    // Riuso trasparente via EntityLink verso il gioco.
                    var linkTargetGameId = session.GameId ?? session.PrivateGameId ?? Guid.Empty;
                    if (linkTargetGameId != Guid.Empty)
                    {
                        try
                        {
                            await _mediator.Send(new CreateEntityLinkCommand(
                                SourceEntityType: MeepleEntityType.Game,
                                SourceEntityId: linkTargetGameId,
                                TargetEntityType: MeepleEntityType.KbCard,
                                TargetEntityId: dedup.ExistingPdfDocumentId!.Value,
                                LinkType: EntityLinkType.RelatedTo,
                                Scope: EntityLinkScope.User,
                                OwnerUserId: session.UserId), cancellationToken).ConfigureAwait(false);
                        }
                        catch (DuplicateEntityLinkException) { /* idempotent */ }
                    }

                    return new CompleteChunkedUploadResult(
                        Success: true,
                        DocumentId: dedup.ExistingPdfDocumentId,
                        FileName: sanitizedFileName,
                        ErrorMessage: null,
                        MissingChunks: null);
                }
            }

            // Create the PDF document record and hand it to the processing queue
            var pdfDocId = await CreatePdfRecordAndEnqueueAsync(
                session, storageResult!, sanitizedFileName!, request.SessionId.ToString(), cancellationToken, contentHash).ConfigureAwait(false);

            return new CompleteChunkedUploadResult(
                Success: true,
                DocumentId: pdfDocId,
                FileName: sanitizedFileName,
                ErrorMessage: null,
                MissingChunks: null
            );
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // COMMAND HANDLER PATTERN: CQRS handler boundary
        // Generic catch handles unexpected failures during chunked upload completion
        // (validation, assembly, storage, DB operations). Returns Result pattern with error.
        // Prevents exception propagation to API layer.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete chunked upload {SessionId}", request.SessionId);

            return new CompleteChunkedUploadResult(
                Success: false,
                DocumentId: null,
                FileName: null,
                ErrorMessage: "Failed to complete upload",
                MissingChunks: null
            );
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validates upload session, permissions, and completeness.
    /// Returns (success, errorMessage, session).
    /// </summary>
    private async Task<(bool success, string? errorMessage, ChunkedUploadSession? uploadSession)> ValidateSessionAndPermissionsAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Get session
        var uploadSession = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (uploadSession == null)
        {
            return (false, "Upload session not found", null);
        }

        // Verify ownership
        if (uploadSession.UserId != userId)
        {
            return (false, "Access denied", uploadSession);
        }

        // Validate TempDirectory to prevent path traversal attacks
        if (!IsValidTempDirectory(uploadSession.TempDirectory))
        {
            _logger.LogWarning(
                "Invalid TempDirectory detected for session {SessionId}: {TempDirectory}",
                sessionId.ToString(), uploadSession.TempDirectory);

            return (false, "Invalid upload session", uploadSession);
        }

        // Check session status
        if (uploadSession.IsExpired)
        {
            uploadSession.MarkAsExpired();
            await _sessionRepository.UpdateAsync(uploadSession, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Cleanup temp files
            await CleanupTempDirectoryAsync(uploadSession.TempDirectory).ConfigureAwait(false);

            return (false, "Upload session has expired", uploadSession);
        }

        if (string.Equals(uploadSession.Status, "completed", StringComparison.Ordinal))
        {
            return (false, "Upload session already completed", uploadSession);
        }

        // Check if all chunks received
        if (!uploadSession.IsComplete)
        {
            var missingChunks = uploadSession.GetMissingChunks();
            return (false, $"Upload incomplete. Missing {missingChunks.Count} chunk(s).", uploadSession);
        }

        return (true, null, uploadSession);
    }

    /// <summary>
    /// Assembles chunks into a single file and stores in blob storage.
    /// Returns (success, storageResult, sanitizedFileName).
    /// </summary>
    private async Task<(bool success, BlobStorageResult? storageResult, string? sanitizedFileName, string? contentHash)> AssembleAndStoreFileAsync(
        ChunkedUploadSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Mark as assembling
        session.MarkAsAssembling();
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Sanitize filename to prevent path traversal attacks
        var sanitizedFileName = PathSecurity.SanitizeFilename(session.FileName);

        // Assemble chunks into a single file
        var assembledFilePath = Path.Combine(session.TempDirectory, sanitizedFileName);
        await AssembleChunksAsync(session, assembledFilePath, cancellationToken).ConfigureAwait(false);

        // Compute content hash from assembled local file (before S3 upload)
        string? contentHash = null;
        if (File.Exists(assembledFilePath))
        {
            var hashStream = new FileStream(assembledFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using (hashStream.ConfigureAwait(false))
            {
                var hashBytes = await SHA256.HashDataAsync(hashStream, cancellationToken).ConfigureAwait(false);
                contentHash = Convert.ToHexStringLower(hashBytes);
            }
        }

        // Store in blob storage
        BlobStorageResult storageResult;
        var assembledStream = new FileStream(assembledFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using (assembledStream.ConfigureAwait(false))
        {
            storageResult = await _blobStorageService.StoreAsync(
                assembledStream,
                sanitizedFileName,
                BlobCategory.Pdf,
                (session.PrivateGameId ?? session.GameId)?.ToString() ?? "wizard-temp",
                cancellationToken).ConfigureAwait(false);
        }

        if (!storageResult.Success)
        {
            session.MarkAsFailed(storageResult.ErrorMessage ?? "Failed to store assembled file");
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return (false, storageResult, sanitizedFileName, null);
        }

        return (true, storageResult, sanitizedFileName, contentHash);
    }

    /// <summary>
    /// Creates the PDF document record, marks the session as completed and hands the document to the
    /// processing queue — record and job commit together, or not at all.
    /// </summary>
    /// <remarks>
    /// #4442: this used to process the PDF itself with a copy of the pipeline that opened
    /// <see cref="BlobStorageResult.FilePath"/> as a local file. With S3 storage that value is the object
    /// key, so every chunked upload ended <c>Failed</c>. The queue pipeline
    /// (<see cref="Services.PdfProcessingPipelineService"/>) reads the file through
    /// <see cref="IBlobStorageService"/> and does more than the copy did (translation, cover, role tags).
    /// The chunked endpoints are admin/editor only, so the job bypasses the queue cap: an interactive
    /// upload is not refused because a bulk reindex filled the queue.
    /// </remarks>
    private async Task<Guid> CreatePdfRecordAndEnqueueAsync(
        ChunkedUploadSession session,
        BlobStorageResult storageResult,
        string sanitizedFileName,
        string sessionId,
        CancellationToken cancellationToken,
        string? contentHash = null)
    {
        // Create PDF document record
        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.Parse(storageResult.FileId!),
            PrivateGameId = session.PrivateGameId,
            // #4395: retrieval finds the documents of a catalogue game by SharedGameId. Without it a
            // PDF uploaded in chunks for a shared game was tied to the game only by the EntityLink
            // below, and invisible to both retrieval arms.
            SharedGameId = session.PrivateGameId is null ? session.GameId : null,
            FileName = sanitizedFileName,
            FilePath = storageResult.FilePath!,
            FileSizeBytes = storageResult.FileSizeBytes,
            ContentType = "application/pdf",
            UploadedByUserId = session.UserId,
            UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
            ContentHash = contentHash
        };

        _dbContext.PdfDocuments.Add(pdfDoc);

        // Mark session as completed
        session.MarkAsCompleted();
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // The production Npgsql context uses a retrying execution strategy, which forbids
        // user-initiated transactions unless they run inside the strategy's executed delegate
        // (same pattern as ReindexDocumentCommandHandler).
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database
                    .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                // Not acceptAllChangesOnSuccess: false — EnqueuePdfCommandHandler saves again on this same
                // context, and a record still in the Added state would be inserted twice (23505). If a
                // transient failure makes the strategy re-run the delegate, the record is not written
                // again and the enqueue no longer finds it (NotFoundException; the job's foreign key would
                // refuse it anyway): the upload fails and the stored object is removed below.
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // EnqueuePdfCommandHandler saves on the shared DbContext under this ambient transaction,
                // so the job commits together with the record.
                await _mediator.Send(
                    new EnqueuePdfCommand(pdfDoc.Id, session.UserId, InteractiveUploadPriority, BypassQueueCap: true),
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch
        {
            // Nothing committed: the stored object would be an orphan. Best effort, like the dedup path.
            // CancellationToken.None: a cancelled request must not leave the object behind as well.
            await DeleteStoredObjectSafelyAsync(storageResult, session, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation(
            "Chunked upload {SessionId} completed. Document {DocumentId} created and enqueued for processing.",
            sessionId, pdfDoc.Id);

        // Issue #5187: Auto-create EntityLink Game → KbCard for PDF-KB association (shared games only)
        await CreateKbCardEntityLinkSafelyAsync(pdfDoc.Id, session.GameId ?? Guid.Empty, session.UserId, cancellationToken).ConfigureAwait(false);

        // Cleanup temp files (async, non-blocking) via background service
        var tempDirToClean = session.TempDirectory;
        _backgroundTaskService.ExecuteWithCancellation(
            $"cleanup_{sessionId}",
            async (cancellationToken) =>
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Small delay to ensure file handles are released
                await CleanupTempDirectoryAsync(tempDirToClean).ConfigureAwait(false);
            });

        return pdfDoc.Id;
    }

    private async Task DeleteStoredObjectSafelyAsync(
        BlobStorageResult storageResult,
        ChunkedUploadSession session,
        CancellationToken cancellationToken)
    {
        if (storageResult.FileId is null)
        {
            return;
        }

        try
        {
            await _blobStorageService.DeleteAsync(storageResult.FileId, BlobCategory.Pdf,
                (session.PrivateGameId ?? session.GameId)?.ToString() ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // CLEANUP PATTERN: the caller is already failing; a cleanup error must not hide the cause.
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete orphaned blob {FileId} after a failed chunked upload completion",
                storageResult.FileId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Assembles all chunk files into a single file.
    /// </summary>
    private async Task AssembleChunksAsync(
        ChunkedUploadSession session,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var outputStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920);
        await using (outputStream.ConfigureAwait(false))
        // 80KB buffer for efficiency
        {
            for (int i = 0; i < session.TotalChunks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunkPath = session.GetChunkFilePath(i);

                if (!File.Exists(chunkPath))
                {
                    throw new InvalidOperationException($"Chunk {i} file not found: {chunkPath}");
                }

                var chunkStream = new FileStream(
                    chunkPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                await using (chunkStream.ConfigureAwait(false))
                {
                    await chunkStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogDebug("Assembled chunk {ChunkIndex}/{TotalChunks}", i + 1, session.TotalChunks);
            }

            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Assembled {TotalChunks} chunks into {OutputPath} ({Size} bytes)",
                session.TotalChunks, outputPath, new FileInfo(outputPath).Length);
        }
    }

    /// <summary>
    /// Cleans up the temporary directory after upload completion or failure.
    /// </summary>
    private Task CleanupTempDirectoryAsync(string tempDirectory)
    {
        try
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
                _logger.LogDebug("Cleaned up temp directory: {TempDirectory}", tempDirectory);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: Best-effort resource cleanup
        // Catches all exceptions during temp directory deletion (permissions, file locks, etc.).
        // Cleanup failure is non-critical; logs warning but doesn't throw to avoid disrupting
        // main operation flow. Fail-safe pattern for resource management.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDirectory}", tempDirectory);
        }
#pragma warning restore CA1031
        return Task.CompletedTask;
    }

    /// <summary>
    /// Auto-creates an EntityLink (Game → KbCard) for the uploaded PDF.
    /// Issue #5187: Idempotent — silently swallows DuplicateEntityLinkException on retry uploads.
    /// Only creates the link for regular game uploads (not private games, identified by gameId != Guid.Empty).
    /// </summary>
    private async Task CreateKbCardEntityLinkSafelyAsync(
        Guid pdfDocumentId,
        Guid gameId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (gameId == Guid.Empty)
            return;

        try
        {
            var cmd = new CreateEntityLinkCommand(
                SourceEntityType: MeepleEntityType.Game,
                SourceEntityId: gameId,
                TargetEntityType: MeepleEntityType.KbCard,
                TargetEntityId: pdfDocumentId,
                LinkType: EntityLinkType.RelatedTo,
                Scope: EntityLinkScope.User,
                OwnerUserId: ownerUserId
            );

            await _mediator.Send(cmd, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "EntityLink Game/{GameId} → KbCard/{PdfId} created for user {UserId}",
                gameId, pdfDocumentId, ownerUserId);
        }
        catch (DuplicateEntityLinkException ex)
        {
            // Idempotent: link already exists (e.g., retry upload). This is expected.
            _logger.LogDebug(
                ex,
                "EntityLink Game/{GameId} → KbCard/{PdfId} already exists — skipping",
                gameId, pdfDocumentId);
        }
        catch (Exception ex)
        {
            // Non-critical: log but do not fail the upload
            _logger.LogWarning(
                ex,
                "Failed to create EntityLink for PDF {PdfId} → Game {GameId}. Upload still succeeded.",
                pdfDocumentId, gameId);
        }
    }

    /// <summary>
    /// Validates that the TempDirectory is within the expected upload base path
    /// and doesn't contain path traversal sequences.
    /// </summary>
    private static bool IsValidTempDirectory(string tempDirectory)
    {
        if (string.IsNullOrWhiteSpace(tempDirectory))
            return false;

        // Check for path traversal sequences
        if (tempDirectory.Contains("..") || tempDirectory.Contains("./"))
            return false;

        // Get the full path to resolve any relative components
        try
        {
            var fullPath = Path.GetFullPath(tempDirectory);
            var basePath = Path.GetFullPath(UploadTempBasePath);

            // Ensure the temp directory is under the base path
            return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If path resolution fails, consider it invalid
            return false;
        }
    }
}

