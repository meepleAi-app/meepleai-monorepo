using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for GameNightPlaylist.
/// Issue #5582: Game Night Playlist backend CRUD with sharing.
/// </summary>
public class GameNightPlaylistEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime? ScheduledDate { get; set; }
    public Guid CreatorUserId { get; set; }

    [MaxLength(50)]
    public string? ShareToken { get; set; }

    public bool IsShared { get; set; }

    /// <summary>
    /// JSON-serialized list of PlaylistGame items (SharedGameId, Position, AddedAt).
    /// </summary>
    public string GamesJson { get; set; } = "[]";

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
