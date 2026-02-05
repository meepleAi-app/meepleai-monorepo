using System.Security.Cryptography;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Represents a card in a deck.
/// </summary>
public class Card
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public string? Suit { get; private set; }
    public string? Value { get; private set; }
    public int SortOrder { get; private set; }

    private Card() { }

    /// <summary>
    /// Creates a custom card.
    /// </summary>
    public static Card Create(string name, string? imageUrl = null, string? suit = null, string? value = null, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Card name is required.", nameof(name));

        return new Card
        {
            Id = Guid.NewGuid(),
            Name = name,
            ImageUrl = imageUrl,
            Suit = suit,
            Value = value,
            SortOrder = sortOrder
        };
    }

    /// <summary>
    /// Creates a standard playing card.
    /// </summary>
    public static Card CreateStandard(string suit, string value, int sortOrder)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            Name = $"{value} of {suit}",
            Suit = suit,
            Value = value,
            SortOrder = sortOrder
        };
    }
}

/// <summary>
/// Standard deck factory for creating 52-card decks.
/// </summary>
public static class StandardDeckFactory
{
    private static readonly string[] Suits = ["Hearts", "Diamonds", "Clubs", "Spades"];
    private static readonly string[] Values = ["Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King"];

    /// <summary>
    /// Creates a standard 52-card deck.
    /// </summary>
    public static List<Card> CreateStandardDeck()
    {
        var cards = new List<Card>(52);
        var sortOrder = 0;

        foreach (var suit in Suits)
        {
            foreach (var value in Values)
            {
                cards.Add(Card.CreateStandard(suit, value, sortOrder++));
            }
        }

        return cards;
    }

    /// <summary>
    /// Creates a standard 52-card deck with jokers.
    /// </summary>
    public static List<Card> CreateStandardDeckWithJokers()
    {
        var cards = CreateStandardDeck();
        cards.Add(Card.Create("Red Joker", suit: "Joker", value: "Joker", sortOrder: 52));
        cards.Add(Card.Create("Black Joker", suit: "Joker", value: "Joker", sortOrder: 53));
        return cards;
    }
}
