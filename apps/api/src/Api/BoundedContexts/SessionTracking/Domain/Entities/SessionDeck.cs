using System.Security.Cryptography;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Represents a card deck in a game session.
/// </summary>
public class SessionDeck
{
    private List<Guid> _drawPile = [];
    private List<Guid> _discardPile = [];
    private Dictionary<Guid, List<Guid>> _hands = [];
    private List<Card> _cards = [];

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DeckType DeckType { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastShuffledAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// All cards in this deck (regardless of location).
    /// </summary>
    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    /// <summary>
    /// Card IDs in the draw pile (top of deck first).
    /// </summary>
    public IReadOnlyList<Guid> DrawPile => _drawPile.AsReadOnly();

    /// <summary>
    /// Card IDs in the discard pile (most recent first).
    /// </summary>
    public IReadOnlyList<Guid> DiscardPile => _discardPile.AsReadOnly();

    /// <summary>
    /// Card IDs in each participant's hand.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Hands =>
        _hands.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Guid>)kvp.Value.AsReadOnly());

    private SessionDeck() { }

    /// <summary>
    /// Creates a new standard 52-card deck.
    /// </summary>
    public static SessionDeck CreateStandardDeck(Guid sessionId, string name = "Standard Deck", bool includeJokers = false)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID is required.", nameof(sessionId));

        var cards = includeJokers
            ? StandardDeckFactory.CreateStandardDeckWithJokers()
            : StandardDeckFactory.CreateStandardDeck();

        var deck = new SessionDeck
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Name = name,
            DeckType = DeckType.Standard52,
            CreatedAt = DateTime.UtcNow,
            _cards = cards,
            _drawPile = cards.Select(c => c.Id).ToList()
        };

        deck.Shuffle();

        return deck;
    }

    /// <summary>
    /// Creates a custom deck with specified cards.
    /// </summary>
    public static SessionDeck CreateCustomDeck(Guid sessionId, string name, List<Card> cards)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID is required.", nameof(sessionId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Deck name is required.", nameof(name));

        if (cards == null || cards.Count == 0)
            throw new ArgumentException("At least one card is required.", nameof(cards));

        var deck = new SessionDeck
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Name = name,
            DeckType = DeckType.Custom,
            CreatedAt = DateTime.UtcNow,
            _cards = cards,
            _drawPile = cards.Select(c => c.Id).ToList()
        };

        deck.Shuffle();

        return deck;
    }

    /// <summary>
    /// Shuffles the draw pile using cryptographic RNG.
    /// </summary>
    public void Shuffle()
    {
        // Fisher-Yates shuffle with cryptographic RNG
        for (var i = _drawPile.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
        }

        LastShuffledAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Shuffles the discard pile back into the draw pile.
    /// </summary>
    public void ShuffleDiscardIntoDraw()
    {
        _drawPile.AddRange(_discardPile);
        _discardPile.Clear();
        Shuffle();
    }

    /// <summary>
    /// Draws cards from the top of the deck to a participant's hand.
    /// </summary>
    public List<Guid> DrawCards(Guid participantId, int count)
    {
        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID is required.", nameof(participantId));

        if (count <= 0)
            throw new ArgumentException("Count must be positive.", nameof(count));

        if (count > _drawPile.Count)
            throw new InvalidOperationException($"Not enough cards in draw pile. Requested: {count}, Available: {_drawPile.Count}");

        // Ensure hand exists
        if (!_hands.ContainsKey(participantId))
            _hands[participantId] = [];

        var drawnCards = _drawPile.Take(count).ToList();
        _drawPile.RemoveRange(0, count);
        _hands[participantId].AddRange(drawnCards);

        return drawnCards;
    }

    /// <summary>
    /// Discards specific cards from a participant's hand.
    /// </summary>
    public void DiscardCards(Guid participantId, List<Guid> cardIds)
    {
        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID is required.", nameof(participantId));

        if (cardIds == null || cardIds.Count == 0)
            throw new ArgumentException("At least one card ID is required.", nameof(cardIds));

        if (!_hands.TryGetValue(participantId, out var hand))
            throw new InvalidOperationException("Participant does not have a hand.");

        foreach (var cardId in cardIds)
        {
            if (!hand.Remove(cardId))
                throw new InvalidOperationException($"Card {cardId} not found in participant's hand.");

            _discardPile.Insert(0, cardId); // Most recent discard on top
        }
    }

    /// <summary>
    /// Returns cards from a participant's hand to the bottom of the draw pile.
    /// </summary>
    public void ReturnCards(Guid participantId, List<Guid> cardIds)
    {
        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID is required.", nameof(participantId));

        if (cardIds == null || cardIds.Count == 0)
            throw new ArgumentException("At least one card ID is required.", nameof(cardIds));

        if (!_hands.TryGetValue(participantId, out var hand))
            throw new InvalidOperationException("Participant does not have a hand.");

        foreach (var cardId in cardIds)
        {
            if (!hand.Remove(cardId))
                throw new InvalidOperationException($"Card {cardId} not found in participant's hand.");

            _drawPile.Add(cardId); // Add to bottom
        }
    }

    /// <summary>
    /// Peeks at the top cards of the draw pile without removing them.
    /// </summary>
    public List<Guid> PeekDrawPile(int count)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be positive.", nameof(count));

        return _drawPile.Take(Math.Min(count, _drawPile.Count)).ToList();
    }

    /// <summary>
    /// Gets a participant's hand.
    /// </summary>
    public List<Guid> GetHand(Guid participantId)
    {
        if (_hands.TryGetValue(participantId, out var hand))
            return hand.ToList();

        return [];
    }

    /// <summary>
    /// Gets the card details by ID.
    /// </summary>
    public Card? GetCard(Guid cardId)
    {
        return _cards.FirstOrDefault(c => c.Id == cardId);
    }

    /// <summary>
    /// Gets multiple card details by IDs.
    /// </summary>
    public List<Card> GetCards(IEnumerable<Guid> cardIds)
    {
        var idSet = cardIds.ToHashSet();
        return _cards.Where(c => idSet.Contains(c.Id)).ToList();
    }

    /// <summary>
    /// Resets the deck: all cards return to draw pile and shuffle.
    /// </summary>
    public void Reset()
    {
        _drawPile = _cards.Select(c => c.Id).ToList();
        _discardPile.Clear();
        _hands.Clear();
        Shuffle();
    }

    /// <summary>
    /// Soft deletes the deck.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    // For EF Core mapping - internal setters
    internal void SetDrawPile(List<Guid> drawPile) => _drawPile = drawPile;
    internal void SetDiscardPile(List<Guid> discardPile) => _discardPile = discardPile;
    internal void SetHands(Dictionary<Guid, List<Guid>> hands) => _hands = hands;
    internal void SetCards(List<Card> cards) => _cards = cards;
}

/// <summary>
/// Type of deck.
/// </summary>
public enum DeckType
{
    Standard52 = 0,
    Standard54 = 1, // With jokers
    Custom = 10
}
