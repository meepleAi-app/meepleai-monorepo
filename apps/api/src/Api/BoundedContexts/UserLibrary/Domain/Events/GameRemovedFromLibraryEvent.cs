using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a game is removed from a user's library.
/// </summary>
internal sealed class GameRemovedFromLibraryEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the library entry that was removed.
    /// </summary>
    public Guid EntryId { get; }

    /// <summary>
    /// The ID of the user who removed the game.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The ID of the game that was removed.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// R2 storage key for the user-uploaded custom cover image (L3, issue #1824).
    /// Null when the library entry had no custom cover at removal time.
    /// Used by <c>GameRemovedFromLibraryCustomCoverHandler</c> to clean up the orphan blob.
    /// </summary>
    public string? CustomCoverR2Key { get; }

    public GameRemovedFromLibraryEvent(Guid entryId, Guid userId, Guid gameId, string? customCoverR2Key = null)
    {
        EntryId = entryId;
        UserId = userId;
        GameId = gameId;
        CustomCoverR2Key = customCoverR2Key;
    }
}
