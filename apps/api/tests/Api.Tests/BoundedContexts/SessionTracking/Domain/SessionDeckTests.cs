using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
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
        deck.Id.Should().NotBe(Guid.Empty);
        deck.SessionId.Should().Be(_sessionId);
        deck.Name.Should().Be("Standard Deck");
        deck.DeckType.Should().Be(DeckType.Standard52);
        deck.Cards.Count.Should().Be(52);
        deck.DrawPile.Count.Should().Be(52);
        deck.DiscardPile.Should().BeEmpty();
    }

    [Fact]
    public void CreateStandardDeck_WithJokers_Returns54Cards()
    {
        // Act
        var deck = SessionDeck.CreateStandardDeck(_sessionId, "Deck with Jokers", includeJokers: true);

        // Assert
        deck.Cards.Count.Should().Be(54);
        deck.DrawPile.Count.Should().Be(54);
        deck.Cards.Should().Contain(c => c.Name == "Red Joker");
        deck.Cards.Should().Contain(c => c.Name == "Black Joker");
    }

    [Fact]
    public void CreateStandardDeck_EmptySessionId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => SessionDeck.CreateStandardDeck(Guid.Empty);
        act.Should().Throw<ArgumentException>();
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
        deck.DeckType.Should().Be(DeckType.Custom);
        deck.Name.Should().Be("My Custom Deck");
        deck.Cards.Count.Should().Be(3);
    }

    [Fact]
    public void CreateCustomDeck_EmptyCards_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => SessionDeck.CreateCustomDeck(_sessionId, "Empty Deck", new List<Card>());
        act.Should().Throw<ArgumentException>();
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
        orderChanged.Should().BeTrue("Shuffle should change the order of cards");
        deck.DrawPile.Count.Should().Be(52);
        deck.LastShuffledAt.Should().NotBeNull();
    }

    [Fact]
    public void DrawCards_ValidCount_MovesCardsToHand()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        var drawnCards = deck.DrawCards(_participantId, 5);

        // Assert
        drawnCards.Count.Should().Be(5);
        deck.DrawPile.Count.Should().Be(47);
        deck.GetHand(_participantId).Count.Should().Be(5);
    }

    [Fact]
    public void DrawCards_TooManyCards_ThrowsInvalidOperationException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act & Assert
        var act = () => deck.DrawCards(_participantId, 100);
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Not enough cards");
    }

    [Fact]
    public void DrawCards_EmptyParticipantId_ThrowsArgumentException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act & Assert
        var act = () => deck.DrawCards(Guid.Empty, 5);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DrawCards_ZeroOrNegativeCount_ThrowsArgumentException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act & Assert
        var act0 = () => deck.DrawCards(_participantId, 0);
        act0.Should().Throw<ArgumentException>();
        var actNeg = () => deck.DrawCards(_participantId, -1);
        actNeg.Should().Throw<ArgumentException>();
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
        deck.GetHand(_participantId).Count.Should().Be(3);
        deck.DiscardPile.Count.Should().Be(2);
    }

    [Fact]
    public void DiscardCards_CardNotInHand_ThrowsInvalidOperationException()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);
        deck.DrawCards(_participantId, 5);

        // Act & Assert
        var act = () => deck.DiscardCards(_participantId, new List<Guid> { Guid.NewGuid() });
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("not found in participant's hand");
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
        deck.GetHand(_participantId).Should().ContainSingle();
        deck.DrawPile.Count.Should().Be(51);
        // Returned cards should be at the bottom
        deck.DrawPile.TakeLast(2).Should().Contain(drawnCards[0]);
        deck.DrawPile.TakeLast(2).Should().Contain(drawnCards[1]);
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
        deck.DrawPile.Count.Should().Be(52);
        deck.DiscardPile.Should().BeEmpty();
    }

    [Fact]
    public void PeekDrawPile_ReturnsTopCards()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        var peeked = deck.PeekDrawPile(3);

        // Assert
        peeked.Count.Should().Be(3);
        deck.DrawPile.Count.Should().Be(52); // Cards not removed
        peeked.Should().BeEquivalentTo(deck.DrawPile.Take(3));
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
        peeked.Count.Should().Be(2);
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
        card.Should().NotBeNull();
        card!.Id.Should().Be(cardId);
    }

    [Fact]
    public void GetCard_NonExistingId_ReturnsNull()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        var card = deck.GetCard(Guid.NewGuid());

        // Assert
        card.Should().BeNull();
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
        deck.DrawPile.Count.Should().Be(52);
        deck.DiscardPile.Should().BeEmpty();
        deck.GetHand(_participantId).Should().BeEmpty();
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var deck = SessionDeck.CreateStandardDeck(_sessionId);

        // Act
        deck.SoftDelete();

        // Assert
        deck.IsDeleted.Should().BeTrue();
        deck.DeletedAt.Should().NotBeNull();
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
        deck.GetHand(participant1).Count.Should().Be(5);
        deck.GetHand(participant2).Count.Should().Be(7);
        deck.DrawPile.Count.Should().Be(40);
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
        card.Id.Should().NotBe(Guid.Empty);
        card.Name.Should().Be("My Card");
        card.ImageUrl.Should().Be("http://example.com/card.jpg");
        card.Suit.Should().Be("Custom");
        card.Value.Should().Be("1");
    }

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        var act1 = () => Card.Create("");
        act1.Should().Throw<ArgumentException>();
        var act2 = () => Card.Create("   ");
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateStandard_ValidSuitAndValue_ReturnsPlayingCard()
    {
        // Act
        var card = Card.CreateStandard("Hearts", "Ace", 0);

        // Assert
        card.Name.Should().Be("Ace of Hearts");
        card.Suit.Should().Be("Hearts");
        card.Value.Should().Be("Ace");
    }

    [Fact]
    public void StandardDeckFactory_CreateStandardDeck_Returns52Cards()
    {
        // Act
        var cards = StandardDeckFactory.CreateStandardDeck();

        // Assert
        cards.Count.Should().Be(52);

        // Verify all suits
        cards.Count(c => c.Suit == "Hearts").Should().Be(13);
        cards.Count(c => c.Suit == "Diamonds").Should().Be(13);
        cards.Count(c => c.Suit == "Clubs").Should().Be(13);
        cards.Count(c => c.Suit == "Spades").Should().Be(13);

        // Verify all values
        foreach (var value in new[] { "Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King" })
        {
            cards.Count(c => c.Value == value).Should().Be(4);
        }
    }

    [Fact]
    public void StandardDeckFactory_CreateStandardDeckWithJokers_Returns54Cards()
    {
        // Act
        var cards = StandardDeckFactory.CreateStandardDeckWithJokers();

        // Assert
        cards.Count.Should().Be(54);
        cards.Count(c => c.Suit == "Joker").Should().Be(2);
    }
}
