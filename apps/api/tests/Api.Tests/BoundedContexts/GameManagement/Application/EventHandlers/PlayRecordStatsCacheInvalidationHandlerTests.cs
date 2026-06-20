using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="PlayRecordStatsCacheInvalidationHandler"/> (#2438, PR-B).
/// The handler evicts the per-user player-statistics cache tag when a record's inclusion
/// in the cached projection changes (see ADR-081):
/// <list type="bullet">
///   <item><description>Completed event — carries no userId, resolved via DB lookup.</description></item>
///   <item><description>Deleted event (#2446) — carries <c>DeletedByUserId</c> directly,
///     no DB lookup needed.</description></item>
/// </list>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2438")]
public sealed class PlayRecordStatsCacheInvalidationHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly Mock<IHybridCacheService> _cache;
    private readonly Mock<ILogger<PlayRecordStatsCacheInvalidationHandler>> _logger;
    private readonly PlayRecordStatsCacheInvalidationHandler _handler;

    public PlayRecordStatsCacheInvalidationHandlerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _cache = new Mock<IHybridCacheService>();
        _logger = new Mock<ILogger<PlayRecordStatsCacheInvalidationHandler>>();
        _handler = new PlayRecordStatsCacheInvalidationHandler(_context, _cache.Object, _logger.Object);

        _cache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Handle_Completed_ResolvesOwnerAndEvictsUserStatsTag()
    {
        // Arrange — seed the completed record so the userId lookup resolves.
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        _context.PlayRecords.Add(MakeRecord(recordId, userId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PlayRecordCompletedEvent(recordId, TimeSpan.FromHours(1)),
            TestContext.Current.CancellationToken);

        // Assert — evicts the owning user's tag.
        _cache.Verify(
            c => c.RemoveByTagAsync($"player-stats:{userId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Completed_RecordNotFound_DoesNotEvict()
    {
        // Arrange — no record seeded → lookup returns Guid.Empty.
        // Act
        await _handler.Handle(
            new PlayRecordCompletedEvent(Guid.NewGuid(), TimeSpan.FromHours(1)),
            TestContext.Current.CancellationToken);

        // Assert — never evicts on an unresolved owner (guards re-dispatch races).
        _cache.Verify(
            c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Completed_NullNotification_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(
            (PlayRecordCompletedEvent)null!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_Completed_CacheThrows_IsSwallowed_BestEffort()
    {
        // Arrange — eviction is best-effort: a cache hiccup must NOT propagate into the
        // CompletePlayRecordCommand that raised the event (mirrors UserActivityCacheInvalidationEventHandler).
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        _context.PlayRecords.Add(MakeRecord(recordId, userId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _cache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        // Act
        var act = () => _handler.Handle(
            new PlayRecordCompletedEvent(recordId, TimeSpan.FromHours(1)),
            TestContext.Current.CancellationToken);

        // Assert — does not throw.
        await act.Should().NotThrowAsync();
    }

    // ─── #2446: PlayRecordDeletedEvent trigger ────────────────────────────────
    // The Deleted event carries DeletedByUserId directly, so the handler skips the DB
    // lookup that Completed needs. Otherwise identical best-effort eviction semantics
    // (ADR-081 deferred hook).

    [Fact]
    [Trait("Issue", "2446")]
    public async Task Handle_Deleted_EvictsDeletedByUserStatsTag()
    {
        // Arrange — no record seed needed: DeletedByUserId is on the event.
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new PlayRecordDeletedEvent(recordId, userId),
            TestContext.Current.CancellationToken);

        // Assert — evicts the deleting user's tag.
        _cache.Verify(
            c => c.RemoveByTagAsync($"player-stats:{userId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Issue", "2446")]
    public async Task Handle_Deleted_EmptyUserId_DoesNotEvict()
    {
        // Arrange — DeletedByUserId == Guid.Empty should be defended against, mirroring
        // the Completed handler's "missing owner" guard (anonymous delete, seed script, etc.).
        // Act
        await _handler.Handle(
            new PlayRecordDeletedEvent(Guid.NewGuid(), Guid.Empty),
            TestContext.Current.CancellationToken);

        // Assert — never evicts on an empty userId (avoids hitting a bogus cache key).
        _cache.Verify(
            c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Issue", "2446")]
    public async Task Handle_Deleted_NullNotification_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(
            (PlayRecordDeletedEvent)null!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Issue", "2446")]
    public async Task Handle_Deleted_CacheThrows_IsSwallowed_BestEffort()
    {
        // Arrange — eviction is best-effort: a cache hiccup must NOT propagate into the
        // DeletePlayRecordCommand that raised the event.
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        _cache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        // Act
        var act = () => _handler.Handle(
            new PlayRecordDeletedEvent(recordId, userId),
            TestContext.Current.CancellationToken);

        // Assert — does not throw.
        await act.Should().NotThrowAsync();
    }

    private static PlayRecordEntity MakeRecord(Guid id, Guid userId) => new()
    {
        Id = id,
        GameId = Guid.NewGuid(),
        GameName = "Test Game",
        CreatedByUserId = userId,
        Visibility = 0,
        SessionDate = DateTime.UtcNow.AddHours(-1),
        Status = 2, // Completed
        ScoringConfigJson = """{"Dimensions":["points","wins"],"Units":{"points":"pts","wins":"W"}}""",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
