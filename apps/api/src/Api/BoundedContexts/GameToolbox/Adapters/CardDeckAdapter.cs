using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;

namespace Api.BoundedContexts.GameToolbox.Adapters;

/// <summary>
/// Facades SessionTracking.SessionDeck with a simplified API for Toolbox usage.
/// Exposes only Shuffle/Draw/Reset — no hands, discard piles, or per-player visibility.
/// </summary>
internal class CardDeckAdapter
{
    private readonly ISessionDeckRepository _deckRepository;

    /// <summary>
    /// Sentinel participant ID used for Toolbox draws (no real participant).
    /// </summary>
    private static readonly Guid ToolboxParticipantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public CardDeckAdapter(ISessionDeckRepository deckRepository)
    {
        _deckRepository = deckRepository ?? throw new ArgumentNullException(nameof(deckRepository));
    }

    /// <summary>
    /// Creates a standard 52-card deck (optionally with jokers).
    /// </summary>
    public async Task<SessionDeck> CreateStandardDeckAsync(
        Guid sessionId, string name, bool includeJokers, CancellationToken cancellationToken)
    {
        var deck = SessionDeck.CreateStandardDeck(sessionId, name, includeJokers);
        await _deckRepository.AddAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return deck;
    }

    /// <summary>
    /// Creates a custom deck with specified cards.
    /// </summary>
    public async Task<SessionDeck> CreateCustomDeckAsync(
        Guid sessionId, string name, List<Card> cards, CancellationToken cancellationToken)
    {
        var deck = SessionDeck.CreateCustomDeck(sessionId, name, cards);
        await _deckRepository.AddAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return deck;
    }

    /// <summary>
    /// Shuffles all cards remaining in the draw pile.
    /// </summary>
    public async Task ShuffleAsync(Guid deckId, CancellationToken cancellationToken)
    {
        var deck = await GetDeckOrThrowAsync(deckId, cancellationToken).ConfigureAwait(false);
        deck.Shuffle();
        await _deckRepository.UpdateAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Draws N cards from the deck. Cards go to a sentinel "hand" (Toolbox mode — visible to all).
    /// Returns the drawn card details.
    /// </summary>
    public async Task<List<Card>> DrawAsync(Guid deckId, int count, CancellationToken cancellationToken)
    {
        var deck = await GetDeckOrThrowAsync(deckId, cancellationToken).ConfigureAwait(false);
        var cardIds = deck.DrawCards(ToolboxParticipantId, count);
        await _deckRepository.UpdateAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return deck.GetCards(cardIds);
    }

    /// <summary>
    /// Returns the number of cards remaining in the draw pile.
    /// </summary>
    public async Task<int> GetRemainingCountAsync(Guid deckId, CancellationToken cancellationToken)
    {
        var deck = await GetDeckOrThrowAsync(deckId, cancellationToken).ConfigureAwait(false);
        return deck.DrawPile.Count;
    }

    /// <summary>
    /// Resets the deck: all cards return to draw pile and shuffle.
    /// </summary>
    public async Task ResetAsync(Guid deckId, CancellationToken cancellationToken)
    {
        var deck = await GetDeckOrThrowAsync(deckId, cancellationToken).ConfigureAwait(false);
        deck.Reset();
        await _deckRepository.UpdateAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SessionDeck> GetDeckOrThrowAsync(Guid deckId, CancellationToken cancellationToken)
    {
        return await _deckRepository.GetByIdAsync(deckId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deck {deckId} not found.");
    }
}
