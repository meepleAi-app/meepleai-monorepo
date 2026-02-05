using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for SessionDeck domain entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionDeckTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _participantId = Guid.NewGuid();

    [Fact]
    public void CreateStandardDeck_ValidSession_Returns52Cards()
    {
        // Act
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Assert
        Assert.NotEqual(Guid.Empty, deck.Id);
        Assert.Equal(_sessionId, deck.SessionId);
        Assert.Equal("Standard Deck", deck.Name);
        Assert.Equal(DeckType.Standard52, deck.DeckType);
        Assert.Equal(52, deck.Cards.Count);
        Assert.Equal(52, deck.DrawPile.Count);
        Assert.Empty(deck.DiscardPile);
    }

    [Fact]
    public void CreateStandardDeck_WithJokers_Returns54Cards()
    {
        // Act
        var deck = SessionDeck.CreateStandardDeck(_sessionId, "Deck with Jokers", includeJokers: true);

        // Assert
        Assert.Equal(54, deck.Cards.Count);
        Assert.Equal(54, deck.DrawPile.Count);
        Assert.Contains(deck.Cards, c => c.Name == "Red Joker");
        Assert.Contains(deck.Cards, c => c.Name == "Black Joker");
    }

    [Fact]
    public void CreateStandardDeck_EmptySessionId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SessionDeck.CreateStandardDeck(Guid.Empty));
    }

    [Fact]
    public void CreateCustomDeck_ValidCards_ReturnsCustomDeck()
    {
        // Arrange
        var cards = new List<Card>
        {
            Card.Create("Card 1", sortOrder: 0),
            Card.Create("Card 2", sortOrder: 1),
            Card.Create("Card 3", sortOrder: 2),
        };

        // Act
        var deck = SessionDeck.CreateCustomDeck(_sessionId, "My Custom Deck", cards);

        // Assert
        Assert.Equal(DeckType.Custom, deck.DeckType);
        Assert.Equal("My Custom Deck", deck.Name);
        Assert.Equal(3, deck.Cards.Count);
    }

    [Fact]
    public void CreateCustomDeck_EmptyCards_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SessionDeck.CreateCustomDeck(_sessionId, "Empty Deck", new List<Card>()));
    }

    [Fact]
    public void Shuffle_ChangesDrawPileOrder()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        var originalOrder = deck.DrawPile.ToList();

        // Act - Shuffle multiple times to ensure order changes
        bool orderChanged = false;
        for (int i = 0; i < 10 && !orderChanged; i++)
        {
            deck.Shuffle();
            orderChanged = !deck.DrawPile.SequenceEqual(originalOrder);
        }

        // Assert
        Assert.True(orderChanged, "Shuffle should change the order of cards");
        Assert.Equal(52, deck.DrawPile.Count);
        Assert.NotNull(deck.LastShuffledAt);
    }

    [Fact]
    public void DrawCards_ValidCount_MovesCardsToHand()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        var drawnCards = deck.DrawCards(_participantId, 5);

        // Assert
        Assert.Equal(5, drawnCards.Count);
        Assert.Equal(47, deck.DrawPile.Count);
        Assert.Equal(5, deck.GetHand(_participantId).Count);
    }

    [Fact]
    public void DrawCards_TooManyCards_ThrowsInvalidOperationException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            deck.DrawCards(_participantId, 100));
        Assert.Contains("Not enough cards", ex.Message);
    }

    [Fact]
    public void DrawCards_EmptyParticipantId_ThrowsArgumentException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            deck.DrawCards(Guid.Empty, 5));
    }

    [Fact]
    public void DrawCards_ZeroOrNegativeCount_ThrowsArgumentException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => deck.DrawCards(_participantId, 0));
        Assert.Throws<ArgumentException>(() => deck.DrawCards(_participantId, -1));
    }

    [Fact]
    public void DiscardCards_ValidCards_MovesToDiscardPile()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        var drawnCards = deck.DrawCards(_participantId, 5);

        // Act
        deck.DiscardCards(_participantId, drawnCards.Take(2).ToList());

        // Assert
        Assert.Equal(3, deck.GetHand(_participantId).Count);
        Assert.Equal(2, deck.DiscardPile.Count);
    }

    [Fact]
    public void DiscardCards_CardNotInHand_ThrowsInvalidOperationException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        deck.DrawCards(_participantId, 5);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            deck.DiscardCards(_participantId, new List<Guid> { Guid.NewGuid() }));
        Assert.Contains("not found in participant's hand", ex.Message);
    }

    [Fact]
    public void ReturnCards_MovesCardsToDeckBottom()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        var drawnCards = deck.DrawCards(_participantId, 3);

        // Act
        deck.ReturnCards(_participantId, drawnCards.Take(2).ToList());

        // Assert
        Assert.Equal(1, deck.GetHand(_participantId).Count);
        Assert.Equal(51, deck.DrawPile.Count);
        // Returned cards should be at the bottom
        Assert.Contains(drawnCards[0], deck.DrawPile.TakeLast(2));
        Assert.Contains(drawnCards[1], deck.DrawPile.TakeLast(2));
    }

    [Fact]
    public void ShuffleDiscardIntoDraw_MergesPiles()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        var drawnCards = deck.DrawCards(_participantId, 10);
        deck.DiscardCards(_participantId, drawnCards);

        // Act
        deck.ShuffleDiscardIntoDraw();

        // Assert
        Assert.Equal(52, deck.DrawPile.Count);
        Assert.Empty(deck.DiscardPile);
    }

    [Fact]
    public void PeekDrawPile_ReturnsTopCards()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        var peeked = deck.PeekDrawPile(3);

        // Assert
        Assert.Equal(3, peeked.Count);
        Assert.Equal(52, deck.DrawPile.Count); // Cards not removed
        Assert.Equal(deck.DrawPile.Take(3), peeked);
    }

    [Fact]
    public void PeekDrawPile_MoreThanAvailable_ReturnsAllAvailable()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        deck.DrawCards(_participantId, 50);

        // Act
        var peeked = deck.PeekDrawPile(10);

        // Assert
        Assert.Equal(2, peeked.Count);
    }

    [Fact]
    public void GetCard_ExistingId_ReturnsCard()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        var cardId = deck.Cards.First().Id;

        // Act
        var card = deck.GetCard(cardId);

        // Assert
        Assert.NotNull(card);
        Assert.Equal(cardId, card.Id);
    }

    [Fact]
    public void GetCard_NonExistingId_ReturnsNull()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        var card = deck.GetCard(Guid.NewGuid());

        // Assert
        Assert.Null(card);
    }

    [Fact]
    public void Reset_ClearsAllAndShuffles()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        var drawnCards = deck.DrawCards(_participantId, 20);
        deck.DiscardCards(_participantId, drawnCards.Take(10).ToList());

        // Act
        deck.Reset();

        // Assert
        Assert.Equal(52, deck.DrawPile.Count);
        Assert.Empty(deck.DiscardPile);
        Assert.Empty(deck.GetHand(_participantId));
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        deck.SoftDelete();

        // Assert
        Assert.True(deck.IsDeleted);
        Assert.NotNull(deck.DeletedAt);
    }

    [Fact]
    public void MultipleParticipants_HaveSeparateHands()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        var participant1 = Guid.NewGuid();
        var participant2 = Guid.NewGuid();

        // Act
        deck.DrawCards(participant1, 5);
        deck.DrawCards(participant2, 7);

        // Assert
        Assert.Equal(5, deck.GetHand(participant1).Count);
        Assert.Equal(7, deck.GetHand(participant2).Count);
        Assert.Equal(40, deck.DrawPile.Count);
    }
}

/// <summary>
/// Unit tests for Card entity and StandardDeckFactory.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class CardTests
{
    [Fact]
    public void Create_ValidCard_ReturnsCard()
    {
        // Act
        var card = Card.Create("My Card", "http://example.com/card.jpg", "Custom", "1", 0);

        // Assert
        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal("My Card", card.Name);
        Assert.Equal("http://example.com/card.jpg", card.ImageUrl);
        Assert.Equal("Custom", card.Suit);
        Assert.Equal("1", card.Value);
    }

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Card.Create(""));
        Assert.Throws<ArgumentException>(() => Card.Create("   "));
    }

    [Fact]
    public void CreateStandard_ValidSuitAndValue_ReturnsPlayingCard()
    {
        // Act
        var card = Card.CreateStandard("Hearts", "Ace", 0);

        // Assert
        Assert.Equal("Ace of Hearts", card.Name);
        Assert.Equal("Hearts", card.Suit);
        Assert.Equal("Ace", card.Value);
    }

    [Fact]
    public void StandardDeckFactory_CreateStandardDeck_Returns52Cards()
    {
        // Act
        var cards = StandardDeckFactory.CreateStandardDeck();

        // Assert
        Assert.Equal(52, cards.Count);

        // Verify all suits
        Assert.Equal(13, cards.Count(c => c.Suit == "Hearts"));
        Assert.Equal(13, cards.Count(c => c.Suit == "Diamonds"));
        Assert.Equal(13, cards.Count(c => c.Suit == "Clubs"));
        Assert.Equal(13, cards.Count(c => c.Suit == "Spades"));

        // Verify all values
        foreach (var value in new[] { "Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King" })
        {
            Assert.Equal(4, cards.Count(c => c.Value == value));
        }
    }

    [Fact]
    public void StandardDeckFactory_CreateStandardDeckWithJokers_Returns54Cards()
    {
        // Act
        var cards = StandardDeckFactory.CreateStandardDeckWithJokers();

        // Assert
        Assert.Equal(54, cards.Count);
        Assert.Equal(2, cards.Count(c => c.Suit == "Joker"));
    }
}
