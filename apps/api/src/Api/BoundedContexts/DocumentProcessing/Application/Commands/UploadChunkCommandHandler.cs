using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for UploadChunkCommand.
/// Saves a chunk to disk and updates session progress.
/// Uses optimistic concurrency with retry logic for concurrent chunk uploads.
/// </summary>
public class UploadChunkCommandHandler : ICommandHandler<UploadChunkCommand, UploadChunkResult>
{
    private const int MaxConcurrencyRetries = 3;
    private static readonly string UploadTempBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");

    private readonly IChunkedUploadSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UploadChunkCommandHandler> _logger;

    public UploadChunkCommandHandler(
        IChunkedUploadSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        ILogger<UploadChunkCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UploadChunkResult> Handle(
        UploadChunkCommand request,
        CancellationToken cancellationToken)
    {
        // Retry loop for handling concurrent chunk uploads
        for (int attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            try
            {
                return await ProcessChunkAsync(request, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    "Concurrency conflict on chunk {ChunkIndex} for session {SessionId}, attempt {Attempt}/{MaxAttempts}",
                    request.ChunkIndex, request.SessionId, attempt + 1, MaxConcurrencyRetries);

                if (attempt == MaxConcurrencyRetries - 1)
                {
                    _logger.LogError(ex,
                        "Max concurrency retries exceeded for chunk {ChunkIndex} session {SessionId}",
                        request.ChunkIndex, request.SessionId);

                    return new UploadChunkResult(
                        Success: false,
                        ReceivedChunks: 0,
                        TotalChunks: 0,
                        ProgressPercentage: 0,
                        IsComplete: false,
                        ErrorMessage: "Concurrent upload conflict. Please retry."
                    );
                }

                // Small delay before retry to reduce contention
                await Task.Delay(10 * (attempt + 1), cancellationToken);
            }
        }

        // Should never reach here, but return error just in case
        return new UploadChunkResult(
            Success: false,
            ReceivedChunks: 0,
            TotalChunks: 0,
            ProgressPercentage: 0,
            IsComplete: false,
            ErrorMessage: "Unexpected error during chunk upload"
        );
    }

    private async Task<UploadChunkResult> ProcessChunkAsync(
        UploadChunkCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get session
            var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);

            if (session == null)
            {
                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: 0,
                    TotalChunks: 0,
                    ProgressPercentage: 0,
                    IsComplete: false,
                    ErrorMessage: "Upload session not found"
                );
            }

            // Verify ownership
            if (session.UserId != request.UserId)
            {
                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: 0,
                    TotalChunks: 0,
                    ProgressPercentage: 0,
                    IsComplete: false,
                    ErrorMessage: "Access denied"
                );
            }

            // Validate TempDirectory to prevent path traversal attacks
            if (!IsValidTempDirectory(session.TempDirectory))
            {
                _logger.LogWarning(
                    "Invalid TempDirectory detected for session {SessionId}: {TempDirectory}",
                    request.SessionId, session.TempDirectory);

                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: 0,
                    TotalChunks: 0,
                    ProgressPercentage: 0,
                    IsComplete: false,
                    ErrorMessage: "Invalid upload session"
                );
            }

            // Check session status
            if (session.IsExpired)
            {
                session.MarkAsExpired();
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: 0,
                    TotalChunks: 0,
                    ProgressPercentage: 0,
                    IsComplete: false,
                    ErrorMessage: "Upload session has expired"
                );
            }

            if (session.Status == "completed" || session.Status == "failed")
            {
                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: session.ReceivedChunks,
                    TotalChunks: session.TotalChunks,
                    ProgressPercentage: session.ProgressPercentage,
                    IsComplete: false,
                    ErrorMessage: $"Session is already {session.Status}"
                );
            }

            // Validate chunk index
            if (request.ChunkIndex < 0 || request.ChunkIndex >= session.TotalChunks)
            {
                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: session.ReceivedChunks,
                    TotalChunks: session.TotalChunks,
                    ProgressPercentage: session.ProgressPercentage,
                    IsComplete: false,
                    ErrorMessage: $"Invalid chunk index. Expected 0-{session.TotalChunks - 1}"
                );
            }

            // Validate chunk size
            if (request.ChunkData.Length > ChunkedUploadSession.MaxChunkSizeBytes)
            {
                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: session.ReceivedChunks,
                    TotalChunks: session.TotalChunks,
                    ProgressPercentage: session.ProgressPercentage,
                    IsComplete: false,
                    ErrorMessage: $"Chunk size exceeds maximum ({ChunkedUploadSession.MaxChunkSizeBytes} bytes)"
                );
            }

            // Check if chunk already received (idempotent)
            if (session.HasChunk(request.ChunkIndex))
            {
                _logger.LogDebug("Chunk {ChunkIndex} already received for session {SessionId}",
                    request.ChunkIndex, request.SessionId);

                return new UploadChunkResult(
                    Success: true,
                    ReceivedChunks: session.ReceivedChunks,
                    TotalChunks: session.TotalChunks,
                    ProgressPercentage: session.ProgressPercentage,
                    IsComplete: session.IsComplete,
                    ErrorMessage: null
                );
            }

            // Ensure temp directory exists
            if (!Directory.Exists(session.TempDirectory))
            {
                Directory.CreateDirectory(session.TempDirectory);
            }

            // Save chunk to disk
            var chunkFilePath = session.GetChunkFilePath(request.ChunkIndex);
            await File.WriteAllBytesAsync(chunkFilePath, request.ChunkData, cancellationToken);

            // Update session
            session.MarkChunkReceived(request.ChunkIndex);
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Chunk {ChunkIndex}/{TotalChunks} received for session {SessionId} ({Progress:F1}%)",
                request.ChunkIndex + 1, session.TotalChunks, request.SessionId, session.ProgressPercentage);

            return new UploadChunkResult(
                Success: true,
                ReceivedChunks: session.ReceivedChunks,
                TotalChunks: session.TotalChunks,
                ProgressPercentage: session.ProgressPercentage,
                IsComplete: session.IsComplete,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process chunk {ChunkIndex} for session {SessionId}",
                request.ChunkIndex, request.SessionId);

            return new UploadChunkResult(
                Success: false,
                ReceivedChunks: 0,
                TotalChunks: 0,
                ProgressPercentage: 0,
                IsComplete: false,
                ErrorMessage: "Failed to process chunk"
            );
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
