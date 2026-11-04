using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to discard cards from a participant's hand.
/// </summary>
public record DiscardCardsCommand : IRequest<DiscardCardsResult>
{
    /// <summary>
    /// Deck ID.
    /// </summary>
    public Guid DeckId { get; init; }

    /// <summary>
    /// Session ID.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Participant ID discarding the cards.
    /// </summary>
    public Guid ParticipantId { get; init; }

    /// <summary>
    /// Card IDs to discard.
    /// </summary>
    public List<Guid> CardIds { get; init; } = [];

    /// <summary>
    /// IDOR guard (#3756): utente autenticato che effettua la chiamata. L'endpoint lo deriva dal
    /// principal e sovrascrive qualunque valore arrivato dal body; l'handler verifica via
    /// <c>ISessionAccessGuard</c> che sia owner o partecipante registrato della sessione.
    /// </summary>
    public Guid RequestedBy { get; init; }
}

/// <summary>
/// Result of discarding cards.
/// </summary>
public record DiscardCardsResult
{
    public Guid DeckId { get; init; }
    public Guid ParticipantId { get; init; }
    public List<CardDto> DiscardedCards { get; init; } = [];
    public int HandSize { get; init; }
}
