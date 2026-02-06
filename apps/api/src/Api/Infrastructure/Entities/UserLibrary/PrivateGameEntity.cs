using Api.BoundedContexts.UserLibrary.Domain.Enums;

namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// PrivateGame entity - persistence model for user's private games.
/// These are games not in the shared catalog, owned by individual users.
/// Issue #3662: Phase 1 - Data Model &amp; Core Infrastructure for Private Games.
/// </summary>
public class PrivateGameEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the user who owns this private game.
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// BoardGameGeek ID if imported from BGG. Null for manual entries.
    /// </summary>
    public int? BggId { get; set; }

    /// <summary>
    /// The title of the game.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Year the game was published.
    /// </summary>
    public int? YearPublished { get; set; }

    /// <summary>
    /// Game description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Minimum number of players.
    /// </summary>
    public int MinPlayers { get; set; }

    /// <summary>
    /// Maximum number of players.
    /// </summary>
    public int MaxPlayers { get; set; }

    /// <summary>
    /// Typical playing time in minutes.
    /// </summary>
    public int? PlayingTimeMinutes { get; set; }

    /// <summary>
    /// Minimum recommended age.
    /// </summary>
    public int? MinAge { get; set; }

    /// <summary>
    /// Complexity rating (1.0-5.0 scale).
    /// </summary>
    public decimal? ComplexityRating { get; set; }

    /// <summary>
    /// URL to the game's main image.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL to the game's thumbnail image.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Source of the private game data (Manual or BoardGameGeek).
    /// </summary>
    public PrivateGameSource Source { get; set; }

    /// <summary>
    /// When BGG data was last synced (for BGG-sourced private games).
    /// </summary>
    public DateTime? BggSyncedAt { get; set; }

    // Audit fields
    /// <summary>
    /// When the private game was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the private game was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Soft delete support
    /// <summary>
    /// Whether the private game has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// When the private game was deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// Navigation property to the owner user.
    /// </summary>
    public UserEntity? Owner { get; set; }

    /// <summary>
    /// Collection of library entries that reference this private game.
    /// </summary>
    public ICollection<UserLibraryEntryEntity> LibraryEntries { get; set; } = new List<UserLibraryEntryEntity>();
}
