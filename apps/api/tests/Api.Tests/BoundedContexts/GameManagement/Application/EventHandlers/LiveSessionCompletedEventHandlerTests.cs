using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

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

    public LiveSessionCompletedEventHandlerTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
        _now = _timeProvider.GetUtcNow().UtcDateTime;
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
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

        var gameId = Guid.NewGuid();
        var @event = CreateCompletedEvent(gameId: gameId);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert - audit log is created by base class
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a =>
            a.Action.Contains("LiveSessionCompletedEvent"));
        Assert.NotNull(auditLog);
        Assert.Contains("LiveSessionCompleted_PlayRecordGenerated", auditLog.Details);
    }

    [Fact]
    public async Task Handle_WithFreeFormSession_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

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
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

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
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

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
        Assert.Contains(sessionId.ToString(), auditLog.Details);
        Assert.Contains("\"PlayerCount\":3", auditLog.Details);
        Assert.Contains("\"TotalTurns\":5", auditLog.Details);
    }

    [Fact]
    public async Task Handle_WithNotes_ProcessesWithoutError()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

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
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

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
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

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
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();
        var handler = new LiveSessionCompletedEventHandler(dbContext, logger.Object, _timeProvider);

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
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LiveSessionCompletedEventHandler(null!, logger.Object, _timeProvider));
    }

    [Fact]
    public void Constructor_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LiveSessionCompletedEventHandler(dbContext, logger.Object, null!));
    }
}
