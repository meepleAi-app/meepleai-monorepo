using Api.BoundedContexts.Authentication.Domain.Enums;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Represents a game suggestion attached to an invitation token.
/// Can be pre-added to the invitee's library or merely suggested.
/// </summary>
internal sealed class InvitationGameSuggestion
{
    public Guid Id { get; private set; }
    public Guid InvitationTokenId { get; private set; }
    public Guid GameId { get; private set; }
    public GameSuggestionType Type { get; private set; }

    // EF Core constructor
    private InvitationGameSuggestion() { }

    /// <summary>
    /// Factory method to create a new game suggestion for an invitation.
    /// </summary>
    /// <param name="invitationTokenId">The parent invitation token ID</param>
    /// <param name="gameId">The game to suggest or pre-add</param>
    /// <param name="type">Whether the game is pre-added or suggested</param>
    /// <returns>New InvitationGameSuggestion instance</returns>
    public static InvitationGameSuggestion Create(Guid invitationTokenId, Guid gameId, GameSuggestionType type)
    {
        if (invitationTokenId == Guid.Empty)
            throw new ArgumentException("Invitation token ID cannot be empty", nameof(invitationTokenId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));

        return new InvitationGameSuggestion
        {
            Id = Guid.NewGuid(),
            InvitationTokenId = invitationTokenId,
            GameId = gameId,
            Type = type
        };
    }
}
