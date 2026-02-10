using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RecordGameEvent;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class RecordGameEventCommandHandlerTests
{
    private readonly Mock<IGameAnalyticsEventRepository> _repositoryMock;
    private readonly Mock<ILogger<RecordGameEventCommandHandler>> _loggerMock;
    private readonly RecordGameEventCommandHandler _handler;

    public RecordGameEventCommandHandlerTests()
    {
        _repositoryMock = new Mock<IGameAnalyticsEventRepository>();
        _loggerMock = new Mock<ILogger<RecordGameEventCommandHandler>>();
        _handler = new RecordGameEventCommandHandler(
            _repositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_PersistsEvent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RecordGameEventCommand(gameId, GameEventType.View, userId);

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<GameAnalyticsEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<GameAnalyticsEvent>(e =>
                e.GameId == gameId &&
                e.EventType == GameEventType.View &&
                e.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullUserId_PersistsAnonymousEvent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new RecordGameEventCommand(gameId, GameEventType.Search, null);

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<GameAnalyticsEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<GameAnalyticsEvent>(e =>
                e.GameId == gameId &&
                e.EventType == GameEventType.Search &&
                e.UserId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(GameEventType.Search)]
    [InlineData(GameEventType.View)]
    [InlineData(GameEventType.LibraryAdd)]
    [InlineData(GameEventType.Play)]
    public async Task Handle_AllEventTypes_PersistsCorrectly(GameEventType eventType)
    {
        // Arrange
        var command = new RecordGameEventCommand(Guid.NewGuid(), eventType);

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<GameAnalyticsEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<GameAnalyticsEvent>(e => e.EventType == eventType),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RecordGameEventCommandHandler(null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RecordGameEventCommandHandler(_repositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
