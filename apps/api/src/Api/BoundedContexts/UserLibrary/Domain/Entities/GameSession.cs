using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Represents a recorded gameplay session for a game in user's library.
/// Tracks when played, duration, outcome, and participants.
/// </summary>
internal sealed class GameSession : Entity<Guid>
{
    /// <summary>
    /// Foreign key to the UserLibraryEntry (aggregate root).
    /// </summary>
    public Guid UserLibraryEntryId { get; private set; }

    /// <summary>
    /// When the session was played.
    /// </summary>
    public DateTime PlayedAt { get; private set; }

    /// <summary>
    /// Duration of the session in minutes.
    /// </summary>
    public int DurationMinutes { get; private set; }

    /// <summary>
    /// Whether the user won this session (null for non-competitive games).
    /// </summary>
    public bool? DidWin { get; private set; }

    /// <summary>
    /// Comma-separated list of players who participated.
    /// </summary>
    public string? Players { get; private set; }

    /// <summary>
    /// Optional notes about the session (memorable moments, rule questions, etc.).
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// When this session record was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When this session record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameSession() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new game session record.
    /// </summary>
    /// <param name="id">Unique identifier for the session</param>
    /// <param name="userLibraryEntryId">The library entry this session belongs to</param>
    /// <param name="playedAt">When the session was played</param>
    /// <param name="durationMinutes">Duration in minutes</param>
    /// <param name="didWin">Whether the user won (null for non-competitive)</param>
    /// <param name="players">List of players</param>
    /// <param name="notes">Session notes</param>
    /// <exception cref="ArgumentException">Thrown when invalid values are provided</exception>
    private GameSession(
        Guid id,
        Guid userLibraryEntryId,
        DateTime playedAt,
        int durationMinutes,
        bool? didWin,
        string? players,
        string? notes) : base(id)
    {
        if (userLibraryEntryId == Guid.Empty)
            throw new ArgumentException("UserLibraryEntryId cannot be empty", nameof(userLibraryEntryId));

        if (durationMinutes <= 0)
            throw new ArgumentException("Duration must be positive", nameof(durationMinutes));

        if (playedAt > DateTime.UtcNow)
            throw new ArgumentException("PlayedAt cannot be in the future", nameof(playedAt));

        UserLibraryEntryId = userLibraryEntryId;
        PlayedAt = playedAt;
        DurationMinutes = durationMinutes;
        DidWin = didWin;
        Players = players?.Trim();
        Notes = notes?.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new game session.
    /// </summary>
    /// <param name="userLibraryEntryId">The library entry this session belongs to</param>
    /// <param name="playedAt">When the session was played</param>
    /// <param name="durationMinutes">Duration in minutes</param>
    /// <param name="didWin">Whether the user won (null for non-competitive)</param>
    /// <param name="players">Comma-separated list of players</param>
    /// <param name="notes">Optional session notes</param>
    /// <returns>New GameSession instance</returns>
    public static GameSession Create(
        Guid userLibraryEntryId,
        DateTime playedAt,
        int durationMinutes,
        bool? didWin = null,
        string? players = null,
        string? notes = null)
    {
        return new GameSession(
            id: Guid.NewGuid(),
            userLibraryEntryId: userLibraryEntryId,
            playedAt: playedAt,
            durationMinutes: durationMinutes,
            didWin: didWin,
            players: players,
            notes: notes);
    }

    /// <summary>
    /// Updates the session notes.
    /// </summary>
    /// <param name="notes">New notes content</param>
    public void UpdateNotes(string? notes)
    {
        Notes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the players list.
    /// </summary>
    /// <param name="players">Comma-separated list of players</param>
    public void UpdatePlayers(string? players)
    {
        Players = players?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the competitive outcome.
    /// </summary>
    /// <param name="didWin">Whether the user won</param>
    public void UpdateOutcome(bool? didWin)
    {
        DidWin = didWin;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Corrects the session duration.
    /// </summary>
    /// <param name="durationMinutes">Corrected duration in minutes</param>
    public void CorrectDuration(int durationMinutes)
    {
        if (durationMinutes <= 0)
            throw new ArgumentException("Duration must be positive", nameof(durationMinutes));

        DurationMinutes = durationMinutes;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns whether this was a competitive session.
    /// </summary>
    public bool IsCompetitive() => DidWin.HasValue;

    /// <summary>
    /// Returns a formatted duration string (e.g., "2h 30m").
    /// </summary>
    public string GetDurationFormatted()
    {
        var hours = DurationMinutes / 60;
        var minutes = DurationMinutes % 60;

        if (hours > 0)
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";

        return $"{minutes}m";
    }

    /// <summary>
    /// Returns the players as a list (split by comma).
    /// </summary>
    public IReadOnlyList<string> GetPlayersList()
    {
        if (string.IsNullOrWhiteSpace(Players))
            return Array.Empty<string>();

        return Players.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
