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

    #region Cache backend down after the commit (the session is already saved)

    // The game is recorded by SaveChanges BEFORE the cache entry is dropped. If dropping it fails (Redis is
    // down) and the request turns into an error, the client retries — React Query retries every mutation
    // once — and the retry records a SECOND session: a double history row and doubled statistics.

    private (RecordGameSessionCommand Command, UserLibraryEntry Entry) GivenARecordableSession()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        var command = new RecordGameSessionCommand(
            UserId: userId, GameId: gameId, PlayedAt: DateTime.UtcNow.AddHours(-1), DurationMinutes: 60);
        return (command, entry);
    }

    private RecordGameSessionCommandHandler HandlerWith(HybridCache cache) =>
        new(_mockLibraryRepo.Object, _mockUnitOfWork.Object, cache, _mockPublisher.Object, _mockLogger.Object);

    [Fact]
    public async Task Handle_WhenTheCacheBackendIsDown_StillReturnsTheSessionId()
    {
        var (command, entry) = GivenARecordableSession();
        var handler = HandlerWith(HybridCacheWithL2Down.Create());

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        result.Should().NotBeEmpty();
        entry.Sessions.Should().ContainSingle(s => s.Id == result);
    }

    [Fact]
    public async Task Handle_WhenTheCacheBackendIsDown_SavesOnce_AndStillPublishesTheEvent()
    {
        var (command, _) = GivenARecordableSession();
        var handler = HandlerWith(HybridCacheWithL2Down.Create());

        await handler.Handle(command, TestContext.Current.CancellationToken);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublisher.Verify(
            p => p.Publish(
                It.IsAny<Api.BoundedContexts.Administration.Domain.Events.UserGameSessionCompletedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the dashboard invalidation event is part of the same request and must not be skipped");
    }

    [Fact]
    public async Task Handle_WhenTheCacheBackendIsDown_LogsAWarning()
    {
        var (command, _) = GivenARecordableSession();
        var handler = HandlerWith(HybridCacheWithL2Down.Create());

        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Not silent: the stale entry stays until it expires, and the operator has to be able to see why.
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheRequestIsCancelledWhileDroppingTheEntry_StillPropagatesTheCancellation()
    {
        var (command, _) = GivenARecordableSession();
        var cache = new Mock<HybridCache>();
        cache
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException(new OperationCanceledException()));
        var handler = HandlerWith(cache.Object);

        var act = () => handler.Handle(command, TestContext.Current.CancellationToken);

        // A cancelled request is not a backend failure: swallowing it would hide the cancellation.
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
