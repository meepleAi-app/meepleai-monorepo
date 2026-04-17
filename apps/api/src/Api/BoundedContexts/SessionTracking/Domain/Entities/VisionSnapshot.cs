using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// A photo snapshot of the board state during a live session.
/// Supports vision AI extraction of game state from images.
/// Session Vision AI feature.
/// </summary>
public class VisionSnapshot
{
    private readonly List<VisionSnapshotImage> _images = [];

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public int TurnNumber { get; private set; }

    [MaxLength(200)]
    public string? Caption { get; private set; }

    public string? ExtractedGameState { get; private set; }
    public IReadOnlyList<VisionSnapshotImage> Images => _images.AsReadOnly();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private VisionSnapshot() { }

    public static VisionSnapshot Create(Guid sessionId, Guid userId, int turnNumber, string? caption)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Session ID required.", nameof(sessionId));
        if (userId == Guid.Empty) throw new ArgumentException("User ID required.", nameof(userId));
        ArgumentOutOfRangeException.ThrowIfNegative(turnNumber);
        if (caption?.Length > 200) throw new ArgumentException("Caption max 200 chars.", nameof(caption));

        return new VisionSnapshot
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            TurnNumber = turnNumber,
            Caption = caption
        };
    }

    public void AddImage(string storageKey, string mediaType, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key required.", nameof(storageKey));

        _images.Add(new VisionSnapshotImage
        {
            Id = Guid.NewGuid(),
            StorageKey = storageKey,
            MediaType = mediaType,
            Width = width,
            Height = height,
            OrderIndex = _images.Count
        });
    }

    public void UpdateGameState(string gameStateJson)
    {
        ExtractedGameState = gameStateJson ?? throw new ArgumentNullException(nameof(gameStateJson));
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// An image attached to a vision snapshot (photo of the board).
/// </summary>
public class VisionSnapshotImage
{
    public Guid Id { get; internal set; }

    [MaxLength(500)]
    public string StorageKey { get; internal set; } = string.Empty;

    [MaxLength(50)]
    public string MediaType { get; internal set; } = string.Empty;

    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public int OrderIndex { get; internal set; }
    public Guid VisionSnapshotId { get; internal set; }
}
