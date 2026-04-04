using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for RecordGameSessionCommandHandler.
/// Verifies game session recording, cache invalidation, and event publishing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class RecordGameSessionCommandHandlerTests : IDisposable
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly HybridCache _cache;
    private readonly Mock<IPublisher> _mockPublisher = new();
    private readonly Mock<ILogger<RecordGameSessionCommandHandler>> _mockLogger = new();
    private readonly RecordGameSessionCommandHandler _handler;

    public RecordGameSessionCommandHandlerTests()
    {
        _cache = TestDbContextFactory.CreateInMemoryHybridCache();
        _handler = new RecordGameSessionCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            _cache,
            _mockPublisher.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        if (_cache is IDisposable disposable)
            disposable.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLibraryRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RecordGameSessionCommandHandler(
            null!,
            _mockUnitOfWork.Object,
            _cache,
            _mockPublisher.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("libraryRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RecordGameSessionCommandHandler(
            _mockLibraryRepo.Object,
            null!,
            _cache,
            _mockPublisher.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RecordGameSessionCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            null!,
            _mockPublisher.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullPublisher_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RecordGameSessionCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            _cache,
            null!,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("publisher");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RecordGameSessionCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            _cache,
            _mockPublisher.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WhenEntryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new RecordGameSessionCommand(
            UserId: userId,
            GameId: gameId,
            PlayedAt: DateTime.UtcNow,
            DurationMinutes: 60);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{gameId}*");
    }

    [Fact]
    public async Task Handle_WithValidEntry_ReturnsSessionId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        var command = new RecordGameSessionCommand(
            UserId: userId,
            GameId: gameId,
            PlayedAt: DateTime.UtcNow.AddHours(-1),
            DurationMinutes: 90,
            DidWin: true,
            Players: "Alice,Bob",
            Notes: "Great game!");

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithValidEntry_PersistsChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        var command = new RecordGameSessionCommand(
            UserId: userId,
            GameId: gameId,
            PlayedAt: DateTime.UtcNow.AddHours(-2),
            DurationMinutes: 45);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLibraryRepo.Verify(
            r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidEntry_PublishesCompletedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        var command = new RecordGameSessionCommand(
            UserId: userId,
            GameId: gameId,
            PlayedAt: DateTime.UtcNow.AddHours(-1),
            DurationMinutes: 60);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(
                It.Is<Api.BoundedContexts.Administration.Domain.Events.UserGameSessionCompletedEvent>(
                    e => e.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
