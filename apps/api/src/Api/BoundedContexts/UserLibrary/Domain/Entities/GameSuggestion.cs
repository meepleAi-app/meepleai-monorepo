using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Domain entity for game suggestions attached to an invitation.
/// Admin Invitation Flow: stores recommended games for invited users.
/// </summary>
internal sealed class GameSuggestion : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid SuggestedByUserId { get; private set; }
    public string? Source { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsDismissed { get; private set; }
    public bool IsAccepted { get; private set; }

    private GameSuggestion() { } // EF Core

    public static GameSuggestion Create(
        Guid userId,
        Guid gameId,
        Guid suggestedByUserId,
        string? source,
        TimeProvider timeProvider)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));
        if (suggestedByUserId == Guid.Empty)
            throw new ArgumentException("SuggestedByUserId cannot be empty", nameof(suggestedByUserId));

        return new GameSuggestion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            SuggestedByUserId = suggestedByUserId,
            Source = source,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            IsDismissed = false,
            IsAccepted = false
        };
    }

    public void Accept()
    {
        IsAccepted = true;
        AddDomainEvent(new GameSuggestionAcceptedEvent(Id, UserId, GameId));
    }

    public void Dismiss()
    {
        IsDismissed = true;
    }
}
