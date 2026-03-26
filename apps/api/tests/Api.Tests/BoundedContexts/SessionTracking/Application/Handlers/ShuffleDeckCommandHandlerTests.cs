using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class ShuffleDeckCommandHandlerTests
{
    private readonly Mock<ISessionDeckRepository> _deckRepoMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly ShuffleDeckCommandHandler _handler;

    public ShuffleDeckCommandHandlerTests()
    {
        var loggerMock = new Mock<ILogger<ShuffleDeckCommandHandler>>();
        _handler = new ShuffleDeckCommandHandler(
            _deckRepoMock.Object, _mediatorMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_DeckNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _deckRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionDeck?)null);

        var command = new ShuffleDeckCommand { DeckId = Guid.NewGuid(), SessionId = Guid.NewGuid() };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeckFromWrongSession_ThrowsForbiddenException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck", false);

        _deckRepoMock.Setup(r => r.GetByIdAsync(deck.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deck);

        var command = new ShuffleDeckCommand
        {
            DeckId = deck.Id,
            SessionId = Guid.NewGuid() // different session
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidDeck_ShufflesAndReturnsResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck", false);

        _deckRepoMock.Setup(r => r.GetByIdAsync(deck.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deck);

        var command = new ShuffleDeckCommand
        {
            DeckId = deck.Id,
            SessionId = sessionId,
            IncludeDiscard = false
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(deck.Id, result.DeckId);
        Assert.Equal(52, result.CardsInDrawPile);
        _deckRepoMock.Verify(r => r.UpdateAsync(deck, It.IsAny<CancellationToken>()), Times.Once);
    }
}
