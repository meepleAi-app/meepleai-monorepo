using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// UserLibraryEntry entity - persistence model for user's game library.
/// Represents the junction between Users and SharedGames (from SharedGameCatalog) with library-specific metadata.
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

    /// <summary>
    /// Custom AI agent configuration (stored as JSONB).
    /// Null means use system default configuration.
    /// </summary>
    public string? CustomAgentConfigJson { get; set; }

    /// <summary>
    /// Custom PDF rulebook URL (overrides SharedGame's PDF).
    /// Null means use SharedGame's default PDF.
    /// </summary>
    public string? CustomPdfUrl { get; set; }

    /// <summary>
    /// When the custom PDF was uploaded.
    /// </summary>
    public DateTime? CustomPdfUploadedAt { get; set; }

    /// <summary>
    /// File size of custom PDF in bytes.
    /// </summary>
    public long? CustomPdfFileSizeBytes { get; set; }

    /// <summary>
    /// Original filename of the custom PDF.
    /// </summary>
    public string? CustomPdfOriginalFileName { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }

    /// <summary>
    /// Navigation property to SharedGameEntity (from SharedGameCatalog).
    /// </summary>
    public SharedGameEntity? SharedGame { get; set; }
}
