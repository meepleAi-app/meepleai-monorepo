using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class CardToolConfigTests
{
    [Fact]
    public void Constructor_WithDefaults_CreatesValidConfig()
    {
        var config = new CardToolConfig("Main Deck", "standard");

        config.Name.Should().Be("Main Deck");
        config.DeckType.Should().Be("standard");
        config.CardCount.Should().Be(52);
        config.Shuffleable.Should().BeTrue();
        config.DefaultZone.Should().Be(CardZone.DrawPile);
        config.DefaultOrientation.Should().Be(CardOrientation.FaceDown);
        config.CardEntries.Should().BeEmpty();
        config.AllowDraw.Should().BeTrue();
        config.AllowDiscard.Should().BeTrue();
        config.AllowPeek.Should().BeFalse();
        config.AllowReturnToDeck.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithAllParams_CreatesFullConfig()
    {
        var entries = new List<CardEntry>
        {
            new("Ace of Spades", "Spades", "A"),
            new("King of Hearts", "Hearts", "K"),
        };

        var config = new CardToolConfig(
            name: "Custom Deck",
            deckType: "custom",
            cardCount: 100,
            shuffleable: false,
            defaultZone: CardZone.TableArea,
            defaultOrientation: CardOrientation.FaceUp,
            cardEntries: entries,
            allowDraw: true,
            allowDiscard: false,
            allowPeek: true,
            allowReturnToDeck: true);

        config.Name.Should().Be("Custom Deck");
        config.DeckType.Should().Be("custom");
        config.CardCount.Should().Be(2); // Derived from entries count
        config.Shuffleable.Should().BeFalse();
        config.DefaultZone.Should().Be(CardZone.TableArea);
        config.DefaultOrientation.Should().Be(CardOrientation.FaceUp);
        config.CardEntries.Count.Should().Be(2);
        config.AllowDraw.Should().BeTrue();
        config.AllowDiscard.Should().BeFalse();
        config.AllowPeek.Should().BeTrue();
        config.AllowReturnToDeck.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCardEntries_CardCountDerivedFromEntries()
    {
        var entries = new List<CardEntry>
        {
            new("Card 1"),
            new("Card 2"),
            new("Card 3"),
        };

        var config = new CardToolConfig("Test", "standard", cardCount: 52, cardEntries: entries);
        config.CardCount.Should().Be(3); // Overrides cardCount param
    }

    [Fact]
    public void Constructor_WithoutCardEntries_UsesCardCountParam()
    {
        var config = new CardToolConfig("Test", "standard", cardCount: 30);
        config.CardCount.Should().Be(30);
        config.CardEntries.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        ((Action)(() => new CardToolConfig("", "standard"))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        ((Action)(() => new CardToolConfig("   ", "standard"))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithCardCountBelowMin_ThrowsArgumentException()
    {
        ((Action)(() => new CardToolConfig("Test", "standard", cardCount: 0))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithCardCountAboveMax_ThrowsArgumentException()
    {
        ((Action)(() => new CardToolConfig("Test", "standard", cardCount: 1001))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullDeckType_DefaultsToStandard()
    {
        var config = new CardToolConfig("Test", null!);
        config.DeckType.Should().Be("standard");
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var config = new CardToolConfig("  My Deck  ", "standard");
        config.Name.Should().Be("My Deck");
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class CardEntryTests
{
    [Fact]
    public void Constructor_WithNameOnly_CreatesEntry()
    {
        var entry = new CardEntry("Wild Card");
        entry.Name.Should().Be("Wild Card");
        entry.Suit.Should().BeNull();
        entry.Rank.Should().BeNull();
        entry.CustomData.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParams_CreatesFullEntry()
    {
        var entry = new CardEntry("Ace of Spades", "Spades", "A", "{\"value\":14}");
        entry.Name.Should().Be("Ace of Spades");
        entry.Suit.Should().Be("Spades");
        entry.Rank.Should().Be("A");
        entry.CustomData.Should().Be("{\"value\":14}");
    }

    [Fact]
    public void Constructor_TrimsFields()
    {
        var entry = new CardEntry("  Ace  ", "  Spades  ", "  A  ");
        entry.Name.Should().Be("Ace");
        entry.Suit.Should().Be("Spades");
        entry.Rank.Should().Be("A");
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        ((Action)(() => new CardEntry(""))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        ((Action)(() => new CardEntry("   "))).Should().Throw<ArgumentException>();
    }
}
