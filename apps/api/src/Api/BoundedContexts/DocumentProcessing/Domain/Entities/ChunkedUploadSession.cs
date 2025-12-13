using Api.SharedKernel.Domain.Entities;
using System.Globalization;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Represents an in-progress chunked upload session for large PDF files.
/// Tracks received chunks and manages the upload lifecycle.
/// </summary>
public sealed class ChunkedUploadSession : AggregateRoot<Guid>
{
    /// <summary>
    /// Maximum chunk size in bytes (10 MB).
    /// </summary>
    public const int MaxChunkSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Session expiration time in hours.
    /// </summary>
    public const int SessionExpirationHours = 24;

    public Guid GameId { get; private set; }
    public Guid UserId { get; private set; }
    public string FileName { get; private set; }
    public long TotalFileSize { get; private set; }
    public int TotalChunks { get; private set; }
    public int ReceivedChunks { get; private set; }
    public string TempDirectory { get; private set; }
    public string Status { get; private set; } // "pending", "uploading", "assembling", "completed", "failed", "expired"
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Track which chunks have been received (stored as JSON array of chunk indices)
    public string ReceivedChunkIndices { get; private set; }

#pragma warning disable CS8618
    private ChunkedUploadSession() : base() { }
#pragma warning restore CS8618

    public ChunkedUploadSession(
        Guid id,
        Guid gameId,
        Guid userId,
        string fileName,
        long totalFileSize,
        string tempDirectory) : base(id)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        if (totalFileSize <= 0)
            throw new ArgumentException("Total file size must be positive", nameof(totalFileSize));

        if (string.IsNullOrWhiteSpace(tempDirectory))
            throw new ArgumentException("Temp directory cannot be empty", nameof(tempDirectory));

        GameId = gameId;
        UserId = userId;
        FileName = fileName;
        TotalFileSize = totalFileSize;
        TotalChunks = (int)Math.Ceiling((double)totalFileSize / MaxChunkSizeBytes);
        ReceivedChunks = 0;
        TempDirectory = tempDirectory;
        Status = "pending";
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddHours(SessionExpirationHours);
        ReceivedChunkIndices = "[]";
    }

    /// <summary>
    /// Marks a chunk as received.
    /// </summary>
    public void MarkChunkReceived(int chunkIndex)
    {
        if (chunkIndex < 0 || chunkIndex >= TotalChunks)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex),
                $"Chunk index must be between 0 and {TotalChunks - 1}");

        if (IsExpired)
            throw new InvalidOperationException("Upload session has expired");

        if (string.Equals(Status, "completed", StringComparison.Ordinal) || string.Equals(Status, "failed", StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot add chunks to session with status '{Status}'");

        // Parse existing indices
        var indices = ParseReceivedIndices();

        if (indices.Contains(chunkIndex))
            return; // Chunk already received (idempotent)

        indices.Add(chunkIndex);
        ReceivedChunkIndices = $"[{string.Join(",", indices.OrderBy(i => i))}]";
        ReceivedChunks = indices.Count;
        Status = "uploading";
    }

    /// <summary>
    /// Checks if a specific chunk has been received.
    /// </summary>
    public bool HasChunk(int chunkIndex)
    {
        return ParseReceivedIndices().Contains(chunkIndex);
    }

    /// <summary>
    /// Gets the list of missing chunk indices.
    /// </summary>
    public IReadOnlyList<int> GetMissingChunks()
    {
        var received = ParseReceivedIndices();
        return Enumerable.Range(0, TotalChunks)
            .Where(i => !received.Contains(i))
            .ToList();
    }

    /// <summary>
    /// Checks if all chunks have been received.
    /// </summary>
    public bool IsComplete => ReceivedChunks == TotalChunks;

    /// <summary>
    /// Checks if the session has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Marks the session as assembling (combining chunks).
    /// </summary>
    public void MarkAsAssembling()
    {
        if (!IsComplete)
            throw new InvalidOperationException("Cannot assemble until all chunks are received");

        Status = "assembling";
    }

    /// <summary>
    /// Marks the session as completed successfully.
    /// </summary>
    public void MarkAsCompleted()
    {
        Status = "completed";
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the session as failed.
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        Status = "failed";
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Marks the session as expired.
    /// </summary>
    public void MarkAsExpired()
    {
        Status = "expired";
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the expected file path for a specific chunk.
    /// </summary>
    public string GetChunkFilePath(int chunkIndex)
    {
        return Path.Combine(TempDirectory, $"chunk_{chunkIndex:D4}.bin");
    }

    /// <summary>
    /// Calculates the progress percentage.
    /// </summary>
    public double ProgressPercentage => TotalChunks > 0 ? (double)ReceivedChunks / TotalChunks * 100 : 0;

    private HashSet<int> ParseReceivedIndices()
    {
        if (string.IsNullOrEmpty(ReceivedChunkIndices) || string.Equals(ReceivedChunkIndices, "[]", StringComparison.Ordinal))
            return new HashSet<int>();

        var trimmed = ReceivedChunkIndices.Trim('[', ']');
        if (string.IsNullOrEmpty(trimmed))
            return new HashSet<int>();

        // FIX MA0011: Use IFormatProvider for culture-aware parsing
        return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture))
            .ToHashSet();
    }
}
