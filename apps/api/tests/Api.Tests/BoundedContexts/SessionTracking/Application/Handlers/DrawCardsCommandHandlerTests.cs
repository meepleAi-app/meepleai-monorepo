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
public sealed class DrawCardsCommandHandlerTests
{
    private readonly Mock<ISessionDeckRepository> _deckRepoMock = new();
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ILogger<DrawCardsCommandHandler>> _loggerMock = new();
    private readonly DrawCardsCommandHandler _handler;

    public DrawCardsCommandHandlerTests()
    {
        _handler = new DrawCardsCommandHandler(
            _deckRepoMock.Object,
            _sessionRepoMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_DeckNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _deckRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionDeck?)null);

        var command = new DrawCardsCommand
        {
            DeckId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ParticipantId = Guid.NewGuid(),
            Count = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeckBelongsToDifferentSession_ThrowsForbiddenException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck", false);

        _deckRepoMock
            .Setup(r => r.GetByIdAsync(deck.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deck);

        var command = new DrawCardsCommand
        {
            DeckId = deck.Id,
            SessionId = Guid.NewGuid(), // different session
            ParticipantId = Guid.NewGuid(),
            Count = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck", false);

        _deckRepoMock
            .Setup(r => r.GetByIdAsync(deck.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deck);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new DrawCardsCommand
        {
            DeckId = deck.Id,
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(),
            Count = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ParticipantNotInSession_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck", false);
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);

        _deckRepoMock
            .Setup(r => r.GetByIdAsync(deck.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deck);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new DrawCardsCommand
        {
            DeckId = deck.Id,
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(), // not in session
            Count = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsDrawCardsResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = Session.Create(userId, gameId, SessionType.Generic);
        var participant = session.Participants.First();
        var sessionId = session.Id;
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck", false);

        _deckRepoMock
            .Setup(r => r.GetByIdAsync(deck.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deck);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new DrawCardsCommand
        {
            DeckId = deck.Id,
            SessionId = sessionId,
            ParticipantId = participant.Id,
            Count = 2
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(deck.Id, result.DeckId);
        Assert.Equal(participant.Id, result.ParticipantId);
        Assert.Equal(2, result.Cards.Count);

        _deckRepoMock.Verify(r => r.UpdateAsync(deck, It.IsAny<CancellationToken>()), Times.Once);
        _deckRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Publish(It.IsAny<Api.BoundedContexts.SessionTracking.Domain.Events.CardsDrawnEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
