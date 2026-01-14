namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// UserLibraryEntry entity - persistence model for user's game library.
/// Represents the junction between Users and Games with library-specific metadata.
/// </summary>
public class UserLibraryEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the user who owns this library entry.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Reference to the game in the library.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// When the game was added to the library.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional personal notes about the game (max 500 characters).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this game is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
    public GameEntity? Game { get; set; }
}
