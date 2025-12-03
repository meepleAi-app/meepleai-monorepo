using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for CompleteChunkedUploadCommand.
/// Assembles chunks into a complete file and triggers PDF processing.
/// </summary>
public class CompleteChunkedUploadCommandHandler : ICommandHandler<CompleteChunkedUploadCommand, CompleteChunkedUploadResult>
{
    private static readonly string UploadTempBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");

    private readonly IChunkedUploadSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly ILogger<CompleteChunkedUploadCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public CompleteChunkedUploadCommandHandler(
        IChunkedUploadSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        IBlobStorageService blobStorageService,
        IBackgroundTaskService backgroundTaskService,
        ILogger<CompleteChunkedUploadCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _sessionRepository = sessionRepository;
        _dbContext = dbContext;
        _blobStorageService = blobStorageService;
        _backgroundTaskService = backgroundTaskService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CompleteChunkedUploadResult> Handle(
        CompleteChunkedUploadCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get session
            var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);

            if (session == null)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Upload session not found",
                    MissingChunks: null
                );
            }

            // Verify ownership
            if (session.UserId != request.UserId)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Access denied",
                    MissingChunks: null
                );
            }

            // Validate TempDirectory to prevent path traversal attacks
            if (!IsValidTempDirectory(session.TempDirectory))
            {
                _logger.LogWarning(
                    "Invalid TempDirectory detected for session {SessionId}: {TempDirectory}",
                    request.SessionId, session.TempDirectory);

                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Invalid upload session",
                    MissingChunks: null
                );
            }

            // Check session status
            if (session.IsExpired)
            {
                session.MarkAsExpired();
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Cleanup temp files
                await CleanupTempDirectoryAsync(session.TempDirectory);

                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Upload session has expired",
                    MissingChunks: null
                );
            }

            if (session.Status == "completed")
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: "Upload session is already completed",
                    MissingChunks: null
                );
            }

            // Check if all chunks received
            if (!session.IsComplete)
            {
                var missingChunks = session.GetMissingChunks();
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: $"Upload incomplete. Missing {missingChunks.Count} chunk(s).",
                    MissingChunks: missingChunks
                );
            }

            // Mark as assembling
            session.MarkAsAssembling();
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Assembling chunked upload {SessionId} ({TotalChunks} chunks, {TotalSize} bytes)",
                request.SessionId, session.TotalChunks, session.TotalFileSize);

            // Sanitize filename to prevent path traversal attacks
            var sanitizedFileName = PathSecurity.SanitizeFilename(session.FileName);

            // Assemble chunks into a single file
            var assembledFilePath = Path.Combine(session.TempDirectory, sanitizedFileName);
            await AssembleChunksAsync(session, assembledFilePath, cancellationToken);

            // Store in blob storage (using already sanitized filename)

            BlobStorageResult storageResult;
            await using (var assembledStream = new FileStream(assembledFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                storageResult = await _blobStorageService.StoreAsync(
                    assembledStream,
                    sanitizedFileName,
                    session.GameId.ToString(),
                    cancellationToken);
            }

            if (!storageResult.Success)
            {
                session.MarkAsFailed(storageResult.ErrorMessage ?? "Failed to store assembled file");
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: storageResult.ErrorMessage ?? "Failed to store assembled file",
                    MissingChunks: null
                );
            }

            // Create PDF document record
            var pdfDoc = new PdfDocumentEntity
            {
                Id = Guid.Parse(storageResult.FileId!),
                GameId = session.GameId,
                FileName = sanitizedFileName,
                FilePath = storageResult.FilePath!,
                FileSizeBytes = storageResult.FileSizeBytes,
                ContentType = "application/pdf",
                UploadedByUserId = session.UserId,
                UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
                ProcessingStatus = "pending"
            };

            _dbContext.PdfDocuments.Add(pdfDoc);

            // Mark session as completed
            session.MarkAsCompleted();
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Chunked upload {SessionId} completed. Document {DocumentId} created.",
                request.SessionId, pdfDoc.Id);

            // Cleanup temp files (async, non-blocking) via background service
            var tempDirToClean = session.TempDirectory;
            _backgroundTaskService.ExecuteWithCancellation(
                $"cleanup_{request.SessionId}",
                async (ct) =>
                {
                    await Task.Delay(100, ct); // Small delay to ensure file handles are released
                    await CleanupTempDirectoryAsync(tempDirToClean);
                });

            // Trigger background processing (same as regular upload)
            _backgroundTaskService.ExecuteWithCancellation(
                storageResult.FileId!,
                (ct) => TriggerPdfProcessingAsync(storageResult.FileId!, storageResult.FilePath!, ct));

            return new CompleteChunkedUploadResult(
                Success: true,
                DocumentId: pdfDoc.Id,
                FileName: pdfDoc.FileName,
                ErrorMessage: null,
                MissingChunks: null
            );
        }
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
    }

    /// <summary>
    /// Assembles all chunk files into a single file.
    /// </summary>
    private async Task AssembleChunksAsync(
        ChunkedUploadSession session,
        string outputPath,
        CancellationToken cancellationToken)
    {
        await using var outputStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920); // 80KB buffer for efficiency

        for (int i = 0; i < session.TotalChunks; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkPath = session.GetChunkFilePath(i);

            if (!File.Exists(chunkPath))
            {
                throw new InvalidOperationException($"Chunk {i} file not found: {chunkPath}");
            }

            await using var chunkStream = new FileStream(
                chunkPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            await chunkStream.CopyToAsync(outputStream, cancellationToken);

            _logger.LogDebug("Assembled chunk {ChunkIndex}/{TotalChunks}", i + 1, session.TotalChunks);
        }

        await outputStream.FlushAsync(cancellationToken);

        _logger.LogInformation(
            "Assembled {TotalChunks} chunks into {OutputPath} ({Size} bytes)",
            session.TotalChunks, outputPath, new FileInfo(outputPath).Length);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDirectory}", tempDirectory);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Placeholder for triggering PDF processing.
    /// The actual processing is handled by UploadPdfCommandHandler's ProcessPdfAsync.
    /// We need to invoke it through background service.
    /// </summary>
    private Task TriggerPdfProcessingAsync(string pdfId, string filePath, CancellationToken ct)
    {
        // This will be picked up by the same background processing as regular uploads
        _logger.LogInformation("PDF processing queued for chunked upload: {PdfId}", pdfId);
        return Task.CompletedTask;
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
