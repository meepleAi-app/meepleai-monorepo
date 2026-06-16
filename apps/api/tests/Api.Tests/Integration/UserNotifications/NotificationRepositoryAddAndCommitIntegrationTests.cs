using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.UserNotifications;

/// <summary>
/// Integration tests for <see cref="NotificationRepository.AddAndCommitAsync"/>
/// using a real Testcontainers PostgreSQL instance (issue #2383 — ADR-068/072
/// follow-up).
///
/// Covers the race-window the dispatcher pre-check
/// <c>ExistsBySourceEventIdAsync</c> can't fully close: when two MediatR event
/// handlers race past the pre-check, the second SaveChangesAsync hits the
/// partial UNIQUE index <c>UX_notifications_user_source_event_id</c> on
/// <c>(user_id, source_event_id) WHERE source_event_id IS NOT NULL</c> and
/// Postgres emits SqlState 23505. AddAndCommitAsync must catch it, detach the
/// unsaved entity, and return false — the in-app row already exists under the
/// winning caller's CorrelationId.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserNotifications")]
[Trait("Issue", "2383")]
public sealed class NotificationRepositoryAddAndCommitIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private INotificationRepository? _repository;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public NotificationRepositoryAddAndCommitIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_notifrepoaddcommit_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // NotificationRepository depends on IUserNotificationBroadcaster (SSE).
        // No-op mock keeps the broadcast side-effect inert during persistence tests.
        services.AddScoped(_ => Mock.Of<IUserNotificationBroadcaster>());
        services.AddScoped<INotificationRepository, NotificationRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<INotificationRepository>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch (NpgsqlException)
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task AddAndCommitAsync_FirstInsertWithSourceEventId_ReturnsTrueAndPersistsRow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        var notification = BuildNotification(userId, sourceEventId);

        // Act
        var committed = await _repository!.AddAndCommitAsync(notification, TestCancellationToken);

        // Assert — repository signals success
        committed.Should().BeTrue("the first insert wins the race and persists the row");

        // And the row is durably stored
        var persisted = await _dbContext!.Set<NotificationEntity>()
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .ToListAsync(TestCancellationToken);

        persisted.Should().ContainSingle(n => n.SourceEventId == sourceEventId);
    }

    [Fact]
    public async Task AddAndCommitAsync_SecondInsertWithSameSourceEventId_ReturnsFalseAndDoesNotDuplicateRow()
    {
        // Arrange — first insert simulates the winning caller
        var userId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        var first = BuildNotification(userId, sourceEventId);
        var initialCommit = await _repository!.AddAndCommitAsync(first, TestCancellationToken);
        initialCommit.Should().BeTrue("baseline: the first caller must succeed for this test to be meaningful");

        // Act — second insert mimics the losing caller in the race: same
        // (user_id, source_event_id) UNIQUE pair, new aggregate id (different
        // CorrelationId). The partial UNIQUE index on
        // (user_id, source_event_id) WHERE source_event_id IS NOT NULL must trip
        // Postgres SqlState 23505, which AddAndCommitAsync must catch.
        var second = BuildNotification(userId, sourceEventId);
        var raceCommit = await _repository!.AddAndCommitAsync(second, TestCancellationToken);

        // Assert — race detected without surfacing DbUpdateException to the caller
        raceCommit.Should().BeFalse(
            "the dispatcher relies on this signal to skip channel queue items "
            + "instead of letting 23505 bubble up through MediatR (issue #2383)");

        // And the table still contains exactly one row for that pair
        var rowsForPair = await _dbContext!.Set<NotificationEntity>()
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.SourceEventId == sourceEventId, TestCancellationToken);

        rowsForPair.Should().Be(1, "the partial UNIQUE index prevents duplicates");
    }

    [Fact]
    public async Task AddAndCommitAsync_NullSourceEventId_AllowsMultipleRowsForSameUser()
    {
        // Arrange — legacy / hand-triggered admin notifications have no source
        // event id, so the partial UNIQUE index does NOT apply. AddAndCommitAsync
        // must let both rows persist (returns true twice).
        var userId = Guid.NewGuid();

        // Act
        var first = await _repository!.AddAndCommitAsync(BuildNotification(userId, sourceEventId: null), TestCancellationToken);
        var second = await _repository!.AddAndCommitAsync(BuildNotification(userId, sourceEventId: null), TestCancellationToken);

        // Assert
        first.Should().BeTrue();
        second.Should().BeTrue();

        var rowsForUser = await _dbContext!.Set<NotificationEntity>()
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.SourceEventId == null, TestCancellationToken);

        rowsForUser.Should().Be(2,
            "the UNIQUE index has 'WHERE source_event_id IS NOT NULL', so null-id rows are unconstrained");
    }

    // ────────────────────────────────────────────────────────────────────
    // AddBatchAndCommitAsync — issue #2392 (phantom-broadcast follow-up)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddBatchAndCommitAsync_PersistsAllRowsAndPublishesPostSave()
    {
        // Arrange — three notifications (different users, no source event id)
        // mirror the loop callers (admin fan-out, achievement evaluation, etc.)
        // that previously used `AddAsync` + external `SaveChangesAsync`.
        var broadcasterMock = new Mock<IUserNotificationBroadcaster>();
        var repo = BuildRepositoryWithBroadcaster(broadcasterMock.Object);

        var batch = new[]
        {
            BuildNotification(Guid.NewGuid(), sourceEventId: null),
            BuildNotification(Guid.NewGuid(), sourceEventId: null),
            BuildNotification(Guid.NewGuid(), sourceEventId: null),
        };

        // Act
        var persisted = await repo.AddBatchAndCommitAsync(batch, TestCancellationToken);

        // Assert — all rows persisted, broadcaster invoked exactly once per row
        persisted.Should().Be(3);

        var rowsCommitted = await _dbContext!.Set<NotificationEntity>()
            .AsNoTracking()
            .CountAsync(TestCancellationToken);
        rowsCommitted.Should().Be(3);

        broadcasterMock.Verify(
            b => b.Publish(It.IsAny<Guid>(), It.IsAny<Api.BoundedContexts.UserNotifications.Application.DTOs.NotificationDto>()),
            Times.Exactly(3),
            "POST-save side-effects must fire once per persisted row");
    }

    [Fact]
    public async Task AddBatchAndCommitAsync_EmptyEnumerable_IsNoOp()
    {
        var broadcasterMock = new Mock<IUserNotificationBroadcaster>();
        var repo = BuildRepositoryWithBroadcaster(broadcasterMock.Object);

        var persisted = await repo.AddBatchAndCommitAsync(Array.Empty<Notification>(), TestCancellationToken);

        persisted.Should().Be(0);
        broadcasterMock.Verify(
            b => b.Publish(It.IsAny<Guid>(), It.IsAny<Api.BoundedContexts.UserNotifications.Application.DTOs.NotificationDto>()),
            Times.Never,
            "empty batch must not invoke side-effects");
    }

    private INotificationRepository BuildRepositoryWithBroadcaster(IUserNotificationBroadcaster broadcaster)
    {
        // Override the default no-op broadcaster registration with a caller-supplied
        // Mock so per-test assertions on Publish() are possible.
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped(_ => broadcaster);
        services.AddScoped<INotificationRepository, NotificationRepository>();

        var sp = services.BuildServiceProvider();
        _dbContext = sp.GetRequiredService<MeepleAiDbContext>();
        return sp.GetRequiredService<INotificationRepository>();
    }

    private static Notification BuildNotification(Guid userId, Guid? sourceEventId)
    {
        return new Notification(
            id: Guid.NewGuid(),
            userId: userId,
            type: NotificationType.ShareRequestCreated,
            severity: NotificationSeverity.Info,
            title: "Race-window dedup test",
            message: "issue #2383 integration regression",
            correlationId: Guid.NewGuid(),
            sourceEventId: sourceEventId);
    }
}
