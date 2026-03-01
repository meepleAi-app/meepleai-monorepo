using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class CardToolConfigTests
{
    [Fact]
    public void Constructor_WithDefaults_CreatesValidConfig()
    {
        var config = new CardToolConfig("Main Deck", "standard");

        Assert.Equal("Main Deck", config.Name);
        Assert.Equal("standard", config.DeckType);
        Assert.Equal(52, config.CardCount);
        Assert.True(config.Shuffleable);
        Assert.Equal(CardZone.DrawPile, config.DefaultZone);
        Assert.Equal(CardOrientation.FaceDown, config.DefaultOrientation);
        Assert.Empty(config.CardEntries);
        Assert.True(config.AllowDraw);
        Assert.True(config.AllowDiscard);
        Assert.False(config.AllowPeek);
        Assert.False(config.AllowReturnToDeck);
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

        Assert.Equal("Custom Deck", config.Name);
        Assert.Equal("custom", config.DeckType);
        Assert.Equal(2, config.CardCount); // Derived from entries count
        Assert.False(config.Shuffleable);
        Assert.Equal(CardZone.TableArea, config.DefaultZone);
        Assert.Equal(CardOrientation.FaceUp, config.DefaultOrientation);
        Assert.Equal(2, config.CardEntries.Count);
        Assert.True(config.AllowDraw);
        Assert.False(config.AllowDiscard);
        Assert.True(config.AllowPeek);
        Assert.True(config.AllowReturnToDeck);
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
        Assert.Equal(3, config.CardCount); // Overrides cardCount param
    }

    [Fact]
    public void Constructor_WithoutCardEntries_UsesCardCountParam()
    {
        var config = new CardToolConfig("Test", "standard", cardCount: 30);
        Assert.Equal(30, config.CardCount);
        Assert.Empty(config.CardEntries);
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CardToolConfig("", "standard"));
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CardToolConfig("   ", "standard"));
    }

    [Fact]
    public void Constructor_WithCardCountBelowMin_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CardToolConfig("Test", "standard", cardCount: 0));
    }

    [Fact]
    public void Constructor_WithCardCountAboveMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CardToolConfig("Test", "standard", cardCount: 1001));
    }

    [Fact]
    public void Constructor_WithNullDeckType_DefaultsToStandard()
    {
        var config = new CardToolConfig("Test", null!);
        Assert.Equal("standard", config.DeckType);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var config = new CardToolConfig("  My Deck  ", "standard");
        Assert.Equal("My Deck", config.Name);
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
        Assert.Equal("Wild Card", entry.Name);
        Assert.Null(entry.Suit);
        Assert.Null(entry.Rank);
        Assert.Null(entry.CustomData);
    }

    [Fact]
    public void Constructor_WithAllParams_CreatesFullEntry()
    {
        var entry = new CardEntry("Ace of Spades", "Spades", "A", "{\"value\":14}");
        Assert.Equal("Ace of Spades", entry.Name);
        Assert.Equal("Spades", entry.Suit);
        Assert.Equal("A", entry.Rank);
        Assert.Equal("{\"value\":14}", entry.CustomData);
    }

    [Fact]
    public void Constructor_TrimsFields()
    {
        var entry = new CardEntry("  Ace  ", "  Spades  ", "  A  ");
        Assert.Equal("Ace", entry.Name);
        Assert.Equal("Spades", entry.Suit);
        Assert.Equal("A", entry.Rank);
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CardEntry(""));
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CardEntry("   "));
    }
}
