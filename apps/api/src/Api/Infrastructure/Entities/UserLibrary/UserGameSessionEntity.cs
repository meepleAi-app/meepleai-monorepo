namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// GameSession entity - persistence model for recorded gameplay sessions.
/// </summary>
public class UserGameSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to UserLibraryEntry (aggregate root).
    /// </summary>
    public Guid UserLibraryEntryId { get; set; }

    /// <summary>
    /// When the session was played.
    /// </summary>
    public DateTime PlayedAt { get; set; }

    /// <summary>
    /// Duration of the session in minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Whether the user won this session (null for non-competitive games).
    /// </summary>
    public bool? DidWin { get; set; }

    /// <summary>
    /// Comma-separated list of players who participated.
    /// </summary>
    public string? Players { get; set; }

    /// <summary>
    /// Optional notes about the session.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// When this session record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this session record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public UserLibraryEntryEntity? UserLibraryEntry { get; set; }
}
