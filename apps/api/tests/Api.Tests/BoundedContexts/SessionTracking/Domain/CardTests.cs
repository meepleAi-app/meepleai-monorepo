using Api.BoundedContexts.SessionTracking.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Additional unit tests for Card entity and StandardDeckFactory.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CardExtendedTests
{
    // --- Card.Create ---

    [Fact]
    public void Create_WithAllParameters_ShouldSetAllProperties()
    {
        // Act
        var card = Card.Create("Ace of Spades", "http://example.com/card.png", "Spades", "Ace", 10);

        // Assert
        card.Name.Should().Be("Ace of Spades");
        card.ImageUrl.Should().Be("http://example.com/card.png");
        card.Suit.Should().Be("Spades");
        card.Value.Should().Be("Ace");
        card.SortOrder.Should().Be(10);
    }

    [Fact]
    public void Create_WithNameOnly_ShouldHaveDefaultNullableFields()
    {
        // Act
        var card = Card.Create("Wild Card");

        // Assert
        card.Id.Should().NotBeEmpty();
        card.ImageUrl.Should().BeNull();
        card.Suit.Should().BeNull();
        card.Value.Should().BeNull();
        card.SortOrder.Should().Be(0);
    }

    [Fact]
    public void Create_WithNullName_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Card.Create(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Card name is required*");
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var card1 = Card.Create("Card One");
        var card2 = Card.Create("Card Two");

        // Assert
        card1.Id.Should().NotBe(card2.Id);
    }

    // --- Card.CreateStandard ---

    [Fact]
    public void CreateStandard_ShouldSetNameFromSuitAndValue()
    {
        // Act
        var card = Card.CreateStandard("Hearts", "King", 5);

        // Assert
        card.Name.Should().Be("King of Hearts");
        card.Suit.Should().Be("Hearts");
        card.Value.Should().Be("King");
        card.SortOrder.Should().Be(5);
        card.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateStandard_ShouldNotSetImageUrl()
    {
        // Act
        var card = Card.CreateStandard("Diamonds", "7", 20);

        // Assert
        card.ImageUrl.Should().BeNull();
    }

    // --- StandardDeckFactory ---

    [Fact]
    public void CreateStandardDeck_ShouldHaveUniqueSortOrders()
    {
        // Act
        var deck = StandardDeckFactory.CreateStandardDeck();

        // Assert
        deck.Select(c => c.SortOrder).Distinct().Should().HaveCount(52);
    }

    [Fact]
    public void CreateStandardDeckWithJokers_JokersShouldHaveSuitJoker()
    {
        // Act
        var deck = StandardDeckFactory.CreateStandardDeckWithJokers();
        var jokers = deck.Where(c => c.Suit == "Joker").ToList();

        // Assert
        jokers.Should().HaveCount(2);
        jokers.Should().Contain(j => j.Name == "Red Joker");
        jokers.Should().Contain(j => j.Name == "Black Joker");
        jokers.All(j => j.Value == "Joker").Should().BeTrue();
    }
}
