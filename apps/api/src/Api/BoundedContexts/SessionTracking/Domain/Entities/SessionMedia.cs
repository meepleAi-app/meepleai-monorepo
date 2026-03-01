namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Represents a media file attached to a game session (photo, note, screenshot).
/// Media can be linked to specific turns/snapshots for contextual reference.
/// Issue #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
public class SessionMedia
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public Guid? SnapshotId { get; private set; }
    public string FileId { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public SessionMediaType MediaType { get; private set; }
    public string? Caption { get; private set; }
    public string? ThumbnailFileId { get; private set; }
    public int? TurnNumber { get; private set; }
    public bool IsSharedWithSession { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private SessionMedia() { }

    /// <summary>
    /// Creates a new session media entry.
    /// </summary>
    public static SessionMedia Create(
        Guid sessionId,
        Guid participantId,
        string fileId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        SessionMediaType mediaType,
        string? caption = null,
        Guid? snapshotId = null,
        int? turnNumber = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID cannot be empty.", nameof(participantId));

        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("File ID cannot be empty.", nameof(fileId));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));

        if (fileSizeBytes <= 0)
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSizeBytes));

        var now = DateTime.UtcNow;
        return new SessionMedia
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParticipantId = participantId,
            FileId = fileId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            MediaType = mediaType,
            Caption = caption?.Trim(),
            SnapshotId = snapshotId,
            TurnNumber = turnNumber,
            IsSharedWithSession = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Updates the caption text.
    /// </summary>
    public void UpdateCaption(string? caption)
    {
        Caption = caption?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the thumbnail file reference after generation.
    /// </summary>
    public void SetThumbnail(string thumbnailFileId)
    {
        if (string.IsNullOrWhiteSpace(thumbnailFileId))
            throw new ArgumentException("Thumbnail file ID cannot be empty.", nameof(thumbnailFileId));

        ThumbnailFileId = thumbnailFileId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Links this media to a specific snapshot.
    /// </summary>
    public void LinkToSnapshot(Guid snapshotId, int? turnNumber = null)
    {
        if (snapshotId == Guid.Empty)
            throw new ArgumentException("Snapshot ID cannot be empty.", nameof(snapshotId));

        SnapshotId = snapshotId;
        if (turnNumber.HasValue) TurnNumber = turnNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Shares or unshares this media with all session participants.
    /// </summary>
    public void SetSharedWithSession(bool shared)
    {
        IsSharedWithSession = shared;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft deletes the media entry.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Types of media that can be attached to a session.
/// </summary>
public enum SessionMediaType
{
    Photo = 0,
    Note = 1,
    Screenshot = 2,
    Video = 3,
    Audio = 4,
    Document = 5,
}
