using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Unit tests for LiveSessionCompletedEventHandler.
/// Issue #4748: Verifies PlayRecord generation from completed live game sessions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveSessionCompletedEventHandlerTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _now;
    private readonly Mock<IPlayRecordRepository> _playRecordRepository;

    public LiveSessionCompletedEventHandlerTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
        _now = _timeProvider.GetUtcNow().UtcDateTime;
        _playRecordRepository = new Mock<IPlayRecordRepository>();
    }

    private LiveSessionCompletedEventHandler CreateHandler(
        Api.Infrastructure.MeepleAiDbContext? dbContext = null,
        Mock<ILogger<LiveSessionCompletedEventHandler>>? logger = null)
    {
        dbContext ??= TestDbContextFactory.CreateInMemoryDbContext();
        logger ??= new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        return new LiveSessionCompletedEventHandler(
            dbContext, logger.Object, _timeProvider, _playRecordRepository.Object);
    }

    private LiveSessionCompletedEvent CreateCompletedEvent(
        Guid? sessionId = null,
        Guid? gameId = null,
        string gameName = "Catan",
        Guid? createdByUserId = null,
        PlayRecordVisibility visibility = PlayRecordVisibility.Private,
        Guid? groupId = null,
        DateTime? startedAt = null,
        string? notes = null,
        IReadOnlyList<CompletedPlayerSnapshot>? players = null,
        IReadOnlyList<CompletedScoreSnapshot>? scores = null)
    {
        var sid = sessionId ?? Guid.NewGuid();
        var uid = createdByUserId ?? Guid.NewGuid();
        var sessionDate = _now.AddHours(-1);
        var completedAt = _now;
        var started = startedAt ?? _now.AddMinutes(-30);

        players ??= new List<CompletedPlayerSnapshot>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Alice", 10, 1),
            new(Guid.NewGuid(), Guid.NewGuid(), "Bob", 5, 2)
        };

        scores ??= new List<CompletedScoreSnapshot>
        {
            new(players[0].PlayerId, "points", 10, "pts"),
            new(players[1].PlayerId, "points", 5, "pts")
        };

        return new LiveSessionCompletedEvent(
            sid, completedAt, 5,
            gameId, gameName, uid, visibility, groupId,
            sessionDate, started, notes,
            players, scores);
    }

    [Fact]
    public async Task Handle_WithGameLinkedSession_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        var gameId = Guid.NewGuid();
        var @event = CreateCompletedEvent(gameId: gameId);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert - audit log is created by base class
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a =>
            a.Action.Contains("LiveSessionCompletedEvent"));
        Assert.NotNull(auditLog);
        auditLog.Details.Should().Contain("LiveSessionCompleted_PlayRecordGenerated");
    }

    [Fact]
    public async Task Handle_WithFreeFormSession_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        var @event = CreateCompletedEvent(gameId: null); // No game linked

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a =>
            a.Action.Contains("LiveSessionCompletedEvent"));
        Assert.NotNull(auditLog);
    }

    [Fact]
    public async Task Handle_DoesNotThrow_EvenWhenInternalErrorOccurs()
    {
        // Arrange
        var handler = CreateHandler();

        // Create event with empty players to test error-resilient processing
        var @event = CreateCompletedEvent(
            players: new List<CompletedPlayerSnapshot>(),
            scores: new List<CompletedScoreSnapshot>());

        // Act - should not throw regardless of internal state
        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(@event, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_AuditMetadata_ContainsSessionDetails()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        var sessionId = Guid.NewGuid();
        var players = new List<CompletedPlayerSnapshot>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Alice", 10, 1),
            new(Guid.NewGuid(), null, "Bob", 5, 2),
            new(Guid.NewGuid(), Guid.NewGuid(), "Charlie", 3, 3)
        };
        var @event = CreateCompletedEvent(sessionId: sessionId, players: players);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert - audit log details contain expected metadata
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a =>
            a.Action.Contains("LiveSessionCompletedEvent"));
        Assert.NotNull(auditLog);
        auditLog.Details.Should().Contain(sessionId.ToString());
        auditLog.Details.Should().Contain("\"PlayerCount\":3");
        auditLog.Details.Should().Contain("\"TotalTurns\":5");
    }

    [Fact]
    public async Task Handle_WithNotes_ProcessesWithoutError()
    {
        // Arrange
        var handler = CreateHandler();
        var @event = CreateCompletedEvent(notes: "Great game session!");

        // Act
        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(@event, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_WithGroupVisibility_ProcessesWithoutError()
    {
        // Arrange
        var handler = CreateHandler();

        var groupId = Guid.NewGuid();
        var @event = CreateCompletedEvent(
            visibility: PlayRecordVisibility.Group,
            groupId: groupId);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(@event, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_WithNoStartedAt_ProcessesWithoutError()
    {
        // Arrange
        var handler = CreateHandler();
        var @event = CreateCompletedEvent(startedAt: null);

        // Act - null StartedAt means no duration is calculated
        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(@event, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_LogsInformationOnProcessing()
    {
        // Arrange
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = CreateHandler(logger: logger);

        var @event = CreateCompletedEvent();

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert - base class logs "Handling domain event" at minimum
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Handling domain event")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_PersistsPlayRecordViaRepository()
    {
        // Arrange
        var handler = CreateHandler();
        var gameId = Guid.NewGuid();
        var @event = CreateCompletedEvent(gameId: gameId);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert - repository AddAsync was called with a PlayRecord
        _playRecordRepository.Verify(
            r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LiveSessionCompletedEventHandler(
                null!, logger.Object, _timeProvider, _playRecordRepository.Object));
    }

    [Fact]
    public void Constructor_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LiveSessionCompletedEventHandler(
                dbContext, logger.Object, null!, _playRecordRepository.Object));
    }

    [Fact]
    public void Constructor_NullPlayRecordRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LiveSessionCompletedEventHandler(
                dbContext, logger.Object, _timeProvider, null!));
    }
}
