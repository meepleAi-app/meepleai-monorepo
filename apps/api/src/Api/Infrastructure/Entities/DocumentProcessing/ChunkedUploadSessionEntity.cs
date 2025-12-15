namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for chunked upload sessions.
/// Tracks in-progress large file uploads split into multiple chunks.
/// </summary>
internal class ChunkedUploadSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = default!;
    public long TotalFileSize { get; set; }
    public int TotalChunks { get; set; }
    public int ReceivedChunks { get; set; }
    public string TempDirectory { get; set; } = default!;
    public string Status { get; set; } = "pending"; // pending, uploading, assembling, completed, failed, expired
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// JSON array of received chunk indices, e.g., "[0,1,2,5]"
    /// </summary>
    public string ReceivedChunkIndices { get; set; } = "[]";

    // Navigation properties
    public GameEntity Game { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
}
