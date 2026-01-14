using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// UserLibraryEntry aggregate root representing a game in a user's personal library.
/// This is the junction between Users (Authentication) and Games (GameManagement) contexts
/// with library-specific metadata like notes and favorites.
/// </summary>
internal sealed class UserLibraryEntry : AggregateRoot<Guid>
{
    /// <summary>
    /// The ID of the user who owns this library entry.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The ID of the game in the library.
    /// </summary>
    public Guid GameId { get; private set; }

    /// <summary>
    /// When the game was added to the library.
    /// </summary>
    public DateTime AddedAt { get; private set; }

    /// <summary>
    /// Optional personal notes about the game.
    /// </summary>
    public LibraryNotes? Notes { get; private set; }

    /// <summary>
    /// Whether this game is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private UserLibraryEntry() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new library entry for a user's game.
    /// </summary>
    /// <param name="id">Unique identifier for the entry</param>
    /// <param name="userId">The user who owns this entry</param>
    /// <param name="gameId">The game being added to the library</param>
    public UserLibraryEntry(Guid id, Guid userId, Guid gameId) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        UserId = userId;
        GameId = gameId;
        AddedAt = DateTime.UtcNow;
        IsFavorite = false;

        AddDomainEvent(new GameAddedToLibraryEvent(id, userId, gameId));
    }

    /// <summary>
    /// Updates the personal notes for this game.
    /// </summary>
    /// <param name="notes">New notes (or null to clear)</param>
    public void UpdateNotes(LibraryNotes? notes)
    {
        Notes = notes;
    }

    /// <summary>
    /// Updates the notes from a string value.
    /// </summary>
    /// <param name="notes">Notes string (or null/empty to clear)</param>
    public void UpdateNotesFromString(string? notes)
    {
        Notes = LibraryNotes.FromNullable(notes);
    }

    /// <summary>
    /// Toggles the favorite status.
    /// </summary>
    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    /// <summary>
    /// Marks this game as a favorite.
    /// </summary>
    public void MarkAsFavorite()
    {
        IsFavorite = true;
    }

    /// <summary>
    /// Removes the favorite mark from this game.
    /// </summary>
    public void RemoveFavorite()
    {
        IsFavorite = false;
    }

    /// <summary>
    /// Sets the favorite status explicitly.
    /// </summary>
    /// <param name="isFavorite">True to mark as favorite, false otherwise</param>
    public void SetFavorite(bool isFavorite)
    {
        IsFavorite = isFavorite;
    }

    /// <summary>
    /// Prepares this entry for removal, raising the appropriate domain event.
    /// Call this before deleting the entry from the repository.
    /// </summary>
    public void PrepareForRemoval()
    {
        AddDomainEvent(new GameRemovedFromLibraryEvent(Id, UserId, GameId));
    }
}
