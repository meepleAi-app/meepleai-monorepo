namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;

/// <summary>
/// Value object representing a game within a playlist, with its position and addition timestamp.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
public sealed record PlaylistGame
{
    public Guid SharedGameId { get; init; }
    public int Position { get; init; }       // 1-based ordering
    public DateTime AddedAt { get; init; }
}
