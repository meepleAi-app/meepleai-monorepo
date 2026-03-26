using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Unit tests for GameSuggestedHandler.
/// Verifies that game suggestions are created idempotently for users.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GameSuggestedHandlerTests
{
    private readonly Mock<IGameSuggestionRepository> _mockGameSuggestionRepo = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly Mock<ILogger<GameSuggestedHandler>> _mockLogger = new();
    private readonly GameSuggestedHandler _handler;

    public GameSuggestedHandlerTests()
    {
        _handler = new GameSuggestedHandler(
            _mockGameSuggestionRepo.Object,
            _timeProvider,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullGameSuggestionRepo_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GameSuggestedHandler(
            null!,
            _timeProvider,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gameSuggestionRepo");
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GameSuggestedHandler(
            _mockGameSuggestionRepo.Object,
            null!,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("timeProvider");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GameSuggestedHandler(
            _mockGameSuggestionRepo.Object,
            _timeProvider,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenSuggestionDoesNotExist_CreatesSuggestion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        _mockGameSuggestionRepo
            .Setup(r => r.ExistsForUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        _mockGameSuggestionRepo.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.UserLibrary.Domain.Entities.GameSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSuggestionAlreadyExists_SkipsCreation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        _mockGameSuggestionRepo
            .Setup(r => r.ExistsForUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        _mockGameSuggestionRepo.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.UserLibrary.Domain.Entities.GameSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenSuggestionDoesNotExist_CompletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        _mockGameSuggestionRepo
            .Setup(r => r.ExistsForUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var act = () => _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_WhenSuggestionAlreadyExists_CompletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        _mockGameSuggestionRepo
            .Setup(r => r.ExistsForUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}
