using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class CreateDeckCommandHandlerTests
{
    private readonly Mock<ISessionDeckRepository> _deckRepoMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly CreateDeckCommandHandler _handler;

    public CreateDeckCommandHandlerTests()
    {
        var loggerMock = new Mock<ILogger<CreateDeckCommandHandler>>();
        _handler = new CreateDeckCommandHandler(
            _deckRepoMock.Object, _mediatorMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_StandardDeck_CreatesAndReturnsResult()
    {
        // Arrange
        var command = new CreateDeckCommand
        {
            SessionId = Guid.NewGuid(),
            Name = "Main Deck",
            DeckType = "standard",
            IncludeJokers = false
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.DeckId);
        Assert.Equal("Main Deck", result.Name);
        Assert.Equal("Standard", result.DeckType);
        Assert.Equal(52, result.CardCount); // Standard deck without jokers
        _deckRepoMock.Verify(r => r.AddAsync(It.IsAny<SessionDeck>(), It.IsAny<CancellationToken>()), Times.Once);
        _deckRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StandardDeckWithJokers_Creates54Cards()
    {
        // Arrange
        var command = new CreateDeckCommand
        {
            SessionId = Guid.NewGuid(),
            Name = "Joker Deck",
            DeckType = "standard",
            IncludeJokers = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(54, result.CardCount);
    }

    [Fact]
    public async Task Handle_CustomDeck_CreatesWithCustomCards()
    {
        // Arrange
        var command = new CreateDeckCommand
        {
            SessionId = Guid.NewGuid(),
            Name = "Custom Deck",
            DeckType = "custom",
            CustomCards = new List<CustomCardInput>
            {
                new() { Name = "Dragon", Suit = "Monster", Value = "10" },
                new() { Name = "Knight", Suit = "Hero", Value = "8" }
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.CardCount);
        Assert.Equal("Custom Deck", result.Name);
    }
}
