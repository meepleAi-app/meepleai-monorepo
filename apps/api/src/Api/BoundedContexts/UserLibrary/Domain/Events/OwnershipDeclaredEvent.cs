using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a user declares ownership of a game in their library.
/// Triggers RAG access grant for the owned game.
/// </summary>
internal sealed class OwnershipDeclaredEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the library entry where ownership was declared.
    /// </summary>
    public Guid EntryId { get; }

    /// <summary>
    /// The user who declared ownership.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The game for which ownership was declared.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// When ownership was declared.
    /// </summary>
    public DateTime DeclaredAt { get; }

    public OwnershipDeclaredEvent(Guid entryId, Guid userId, Guid gameId, DateTime declaredAt)
    {
        EntryId = entryId;
        UserId = userId;
        GameId = gameId;
        DeclaredAt = declaredAt;
    }
}
