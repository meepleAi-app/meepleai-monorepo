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
/// Issue #1534: Audit persistence is now centralised in <c>DomainEventAuditHandler</c> and is covered
/// by <c>DomainEventAuditHandlerTests</c>; handler-side effects (PlayRecord repository write + logging)
/// remain under test here.
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
        exception.Should().BeNull();
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
        exception.Should().BeNull();
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
        exception.Should().BeNull();
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
        exception.Should().BeNull();
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

    // ── #4156 fetta 2 — the play record carries the totals of the live session ──────────────────

    [Fact]
    public async Task Handle_WithTheWizardDimension_PersistsThePlayRecordWithThatDimension()
    {
        // The desktop wizard's default dimension is «Punti». The play record used the default
        // scoring config, which enables only "points": recording «Punti» threw, the handler
        // swallowed it, and no play record was written.
        PlayRecord? persisted = null;
        _playRecordRepository
            .Setup(r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()))
            .Callback<PlayRecord, CancellationToken>((record, _) => persisted = record)
            .Returns(Task.CompletedTask);
        var marco = new CompletedPlayerSnapshot(Guid.NewGuid(), null, "Marco", 10, 2);
        var giulia = new CompletedPlayerSnapshot(Guid.NewGuid(), null, "Giulia", 30, 1);
        var @event = CreateCompletedEvent(
            gameId: Guid.NewGuid(),
            players: [marco, giulia],
            scores: [new(marco.PlayerId, "Punti", 10, "PV"), new(giulia.PlayerId, "Punti", 30, "PV")]);

        await CreateHandler().Handle(@event, CancellationToken.None);

        persisted.Should().NotBeNull("a completed live session must produce its play record");
        persisted!.Players.Single(p => p.DisplayName == "Marco").GetScore("Punti")!.Value.Should().Be(10);
        persisted.Players.Single(p => p.DisplayName == "Giulia").GetScore("Punti")!.Value.Should().Be(30);
    }

    [Fact]
    public async Task Handle_WithSeveralRounds_RecordsTheTotalOfEachDimension()
    {
        // A play record keeps one value per player and dimension: recording each round replaced
        // the previous one, so the record carried the last round instead of the total.
        PlayRecord? persisted = null;
        _playRecordRepository
            .Setup(r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()))
            .Callback<PlayRecord, CancellationToken>((record, _) => persisted = record)
            .Returns(Task.CompletedTask);
        var marco = new CompletedPlayerSnapshot(Guid.NewGuid(), null, "Marco", 20, 1);
        var giulia = new CompletedPlayerSnapshot(Guid.NewGuid(), null, "Giulia", 10, 2);
        var @event = CreateCompletedEvent(
            gameId: Guid.NewGuid(),
            players: [marco, giulia],
            scores:
            [
                new(marco.PlayerId, "points", 10, "pts"),
                new(marco.PlayerId, "points", 4, "pts"),
                new(marco.PlayerId, "points", 6, "pts"),
                new(giulia.PlayerId, "points", 5, "pts"),
                new(giulia.PlayerId, "points", 5, "pts"),
            ]);

        await CreateHandler().Handle(@event, CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.Players.Single(p => p.DisplayName == "Marco").GetScore("points")!.Value.Should().Be(20);
        persisted.Players.Single(p => p.DisplayName == "Giulia").GetScore("points")!.Value.Should().Be(10);
    }

    [Fact]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        var act = () =>
            new LiveSessionCompletedEventHandler(
                null!, logger.Object, _timeProvider, _playRecordRepository.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        var act = () =>
            new LiveSessionCompletedEventHandler(
                dbContext, logger.Object, null!, _playRecordRepository.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullPlayRecordRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<LiveSessionCompletedEventHandler>>();

        // Act & Assert
        var act = () =>
            new LiveSessionCompletedEventHandler(
                dbContext, logger.Object, _timeProvider, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
