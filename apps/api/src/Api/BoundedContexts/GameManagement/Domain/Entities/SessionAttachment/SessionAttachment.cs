namespace Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

/// <summary>
/// Represents a photo of board state captured during a live game session.
/// Supports multi-day games where players need to restore board state.
/// Issue #5359 - SessionAttachment domain entity.
/// </summary>
#pragma warning disable MA0049
public class SessionAttachment
#pragma warning restore MA0049
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public int? SnapshotIndex { get; private set; }
    public Guid PlayerId { get; private set; }
    public AttachmentType AttachmentType { get; private set; }
    public string BlobUrl { get; private set; } = string.Empty;
    public string? ThumbnailUrl { get; private set; }
    public string? Caption { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private SessionAttachment() { }

    /// <summary>
    /// Creates a new session attachment for a photo captured during a live game session.
    /// </summary>
    public static SessionAttachment Create(
        Guid sessionId,
        Guid playerId,
        AttachmentType attachmentType,
        string blobUrl,
        string contentType,
        long fileSizeBytes,
        string? thumbnailUrl = null,
        string? caption = null,
        int? snapshotIndex = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (playerId == Guid.Empty)
            throw new ArgumentException("Player ID cannot be empty.", nameof(playerId));

        if (string.IsNullOrWhiteSpace(blobUrl))
            throw new ArgumentException("Blob URL cannot be empty.", nameof(blobUrl));

        if (blobUrl.Length > 2048)
            throw new ArgumentException("Blob URL cannot exceed 2048 characters.", nameof(blobUrl));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));

        if (contentType is not ("image/jpeg" or "image/png"))
            throw new ArgumentException("Content type must be 'image/jpeg' or 'image/png'.", nameof(contentType));

        if (fileSizeBytes < 1024)
            throw new ArgumentException("File size must be at least 1KB (1024 bytes).", nameof(fileSizeBytes));

        if (fileSizeBytes > 10_485_760)
            throw new ArgumentException("File size cannot exceed 10MB (10485760 bytes).", nameof(fileSizeBytes));

        if (caption is { Length: > 200 })
            throw new ArgumentException("Caption cannot exceed 200 characters.", nameof(caption));

        if (thumbnailUrl is { Length: > 2048 })
            throw new ArgumentException("Thumbnail URL cannot exceed 2048 characters.", nameof(thumbnailUrl));

        if (!Enum.IsDefined(attachmentType))
            throw new ArgumentException("Invalid attachment type.", nameof(attachmentType));

        return new SessionAttachment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            PlayerId = playerId,
            AttachmentType = attachmentType,
            BlobUrl = blobUrl,
            ThumbnailUrl = thumbnailUrl,
            Caption = caption?.Trim(),
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            SnapshotIndex = snapshotIndex,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        };
    }

    /// <summary>
    /// Sets the thumbnail URL after thumbnail generation.
    /// </summary>
    public void SetThumbnail(string thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            throw new ArgumentException("Thumbnail URL cannot be empty.", nameof(thumbnailUrl));

        if (thumbnailUrl.Length > 2048)
            throw new ArgumentException("Thumbnail URL cannot exceed 2048 characters.", nameof(thumbnailUrl));

        ThumbnailUrl = thumbnailUrl;
    }

    /// <summary>
    /// Updates the caption text.
    /// </summary>
    public void UpdateCaption(string? caption)
    {
        if (caption is { Length: > 200 })
            throw new ArgumentException("Caption cannot exceed 200 characters.", nameof(caption));

        Caption = caption?.Trim();
    }

    /// <summary>
    /// Links this attachment to a specific snapshot index.
    /// </summary>
    public void LinkToSnapshot(int snapshotIndex)
    {
        if (snapshotIndex < 0)
            throw new ArgumentException("Snapshot index cannot be negative.", nameof(snapshotIndex));

        SnapshotIndex = snapshotIndex;
    }

    /// <summary>
    /// Soft deletes the attachment.
    /// </summary>
    public void MarkAsDeleted()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
