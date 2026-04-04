using Api.BoundedContexts.SessionTracking.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CardDrawTests
{
    // CardDraw is a Phase 2 placeholder with public constructor (EF Core scaffold).
    // Tests cover the public constructor and direct property assignment.

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Act
        var cardDraw = new CardDraw();

        // Assert
        cardDraw.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_DefaultTimestamp_ShouldBeApproximatelyUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var cardDraw = new CardDraw();

        // Assert
        var after = DateTime.UtcNow.AddSeconds(1);
        cardDraw.Timestamp.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void Constructor_DefaultDeckType_ShouldBeEmpty()
    {
        // Act
        var cardDraw = new CardDraw();

        // Assert
        cardDraw.DeckType.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_DefaultCardValue_ShouldBeEmpty()
    {
        // Act
        var cardDraw = new CardDraw();

        // Assert
        cardDraw.CardValue.Should().BeEmpty();
    }

    [Fact]
    public void Properties_CanBeSetViaObjectInitializer()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var id = Guid.NewGuid();

        // Act
        var cardDraw = new CardDraw
        {
            Id = id,
            SessionId = sessionId,
            ParticipantId = participantId,
            DeckId = deckId
        };

        // Assert
        cardDraw.Id.Should().Be(id);
        cardDraw.SessionId.Should().Be(sessionId);
        cardDraw.ParticipantId.Should().Be(participantId);
        cardDraw.DeckId.Should().Be(deckId);
    }

    [Fact]
    public void DeckId_DefaultValue_ShouldBeNull()
    {
        // Act
        var cardDraw = new CardDraw();

        // Assert
        cardDraw.DeckId.Should().BeNull();
    }

    [Fact]
    public void TwoInstances_ShouldBeIndependent()
    {
        // Act
        var draw1 = new CardDraw { Id = Guid.NewGuid() };
        var draw2 = new CardDraw { Id = Guid.NewGuid() };

        // Assert
        draw1.Id.Should().NotBe(draw2.Id);
    }
}
