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
                return await ProcessChunkAsync(request, cancellationToken).ConfigureAwait(false);
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
                await Task.Delay(10 * (attempt + 1), cancellationToken).ConfigureAwait(false);
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
            // Validate session and permissions
            (bool sessionValid, ChunkedUploadSession? session, string? sessionError) = await ValidateChunkSessionAsync(
                request.SessionId, request.UserId, cancellationToken).ConfigureAwait(false);
            if (!sessionValid)
            {
                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: 0,
                    TotalChunks: 0,
                    ProgressPercentage: 0,
                    IsComplete: false,
                    ErrorMessage: sessionError
                );
            }

            // Validate chunk parameters
            var (chunkValid, chunkError) = ValidateChunkParameters(
                request.ChunkIndex, request.ChunkData.Length, session!);
            if (!chunkValid)
            {
                return new UploadChunkResult(
                    Success: false,
                    ReceivedChunks: session.ReceivedChunks,
                    TotalChunks: session.TotalChunks,
                    ProgressPercentage: session.ProgressPercentage,
                    IsComplete: false,
                    ErrorMessage: chunkError
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

            // Save chunk and update session
            await SaveChunkAndUpdateSessionAsync(session, request.ChunkIndex, request.ChunkData, cancellationToken).ConfigureAwait(false);

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
    /// Validates upload session, ownership, and status.
    /// Returns (valid, session, errorMessage).
    /// </summary>
    private async Task<(bool valid, ChunkedUploadSession? session, string? errorMessage)> ValidateChunkSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Get session
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return (false, null, "Upload session not found");
        }

        // Verify ownership
        if (session.UserId != userId)
        {
            return (false, null, "Access denied");
        }

        // Validate TempDirectory to prevent path traversal attacks
        if (!IsValidTempDirectory(session.TempDirectory))
        {
            _logger.LogWarning(
                "Invalid TempDirectory detected for session {SessionId}: {TempDirectory}",
                sessionId.ToString(), session.TempDirectory);

            return (false, session, "Invalid upload session");
        }

        // Check session status
        if (session.IsExpired)
        {
            session.MarkAsExpired();
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return (false, session, "Upload session has expired");
        }

        if (string.Equals(session.Status, "completed", StringComparison.Ordinal) || 
            string.Equals(session.Status, "failed", StringComparison.Ordinal))
        {
            return (false, session, $"Session is already {session.Status}");
        }

        return (true, session, null);
    }

    /// <summary>
    /// Validates chunk index and size.
    /// Returns (valid, errorMessage).
    /// </summary>
    private (bool valid, string? errorMessage) ValidateChunkParameters(
        int chunkIndex,
        int chunkDataLength,
        ChunkedUploadSession session)
    {
        // Validate chunk index
        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            return (false, $"Invalid chunk index. Expected 0-{session.TotalChunks - 1}");
        }

        // Validate chunk size
        if (chunkDataLength > ChunkedUploadSession.MaxChunkSizeBytes)
        {
            return (false, $"Chunk size exceeds maximum ({ChunkedUploadSession.MaxChunkSizeBytes} bytes)");
        }

        return (true, null);
    }

    /// <summary>
    /// Saves chunk to disk and updates session progress.
    /// </summary>
    private async Task SaveChunkAndUpdateSessionAsync(
        ChunkedUploadSession session,
        int chunkIndex,
        byte[] chunkData,
        CancellationToken cancellationToken)
    {
        // Ensure temp directory exists
        if (!Directory.Exists(session.TempDirectory))
        {
            Directory.CreateDirectory(session.TempDirectory);
        }

        // Save chunk to disk
        var chunkFilePath = session.GetChunkFilePath(chunkIndex);
        await File.WriteAllBytesAsync(chunkFilePath, chunkData, cancellationToken).ConfigureAwait(false);

        // Update session
        session.MarkChunkReceived(chunkIndex);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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