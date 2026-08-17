using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundJobs;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DomainEventOutbox;

/// <summary>
/// Issue #1966 — integration tests for <see cref="DomainEventOutboxRetentionService"/>.
///
/// <para>Acceptance criteria:</para>
/// <list type="bullet">
///   <item>Only <c>Status == Sent</c> rows with <c>DispatchedAt</c> older than the
///         retention cutoff are deleted.</item>
///   <item>Failed and Pending rows are NEVER deleted by the retention service.</item>
///   <item>Sent rows with <c>DispatchedAt</c> inside the retention window survive.</item>
///   <item>The <c>meepleai_domain_event_outbox_purged_total</c> counter is incremented
///         once per deleted row, tagged by <c>event_type</c>.</item>
///   <item>The chunked DELETE drains an arbitrarily large eligible set without exceeding
///         the configured batch ceiling per round-trip.</item>
/// </list>
///
/// <para>Test harness mirrors <c>DomainEventOutboxProcessorIntegrationTests</c>:
/// Testcontainers Postgres (<c>Integration-GroupD</c>), real DbContext + retention service
/// resolved from a controlled DI container, <see cref="FakeTimeProvider"/> so the cutoff
/// math is deterministic.</para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Issue", "1966")]
public sealed class DomainEventOutboxRetentionServiceIntegrationTests : IAsyncLifetime
{
    private const string FakeEventAlias = "test.fake.event";
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private FakeTimeProvider? _timeProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DomainEventOutboxRetentionServiceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_outbox_retention_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        _timeProvider = new FakeTimeProvider(FixedNow);
        services.AddSingleton<TimeProvider>(_timeProvider);

        // Default options: 30-day SentRetentionDays, 1-hour RetentionIntervalHours (the
        // poll cadence). Tests drive the service through RunOnceAsync so the poll interval
        // never fires.
        services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
            Options.Create(new DomainEventOutboxOptions
            {
                Mode = DomainEventDispatchMode.OutboxOnly,
                SentRetentionDays = 30,
                RetentionIntervalHours = 1,
                RetentionBatchSize = 10_000,
            }));

        services.AddSingleton<DomainEventOutboxRetentionService>();

        _serviceProvider = services.BuildServiceProvider();

        using var bootstrapScope = _serviceProvider.CreateScope();
        var bootstrapDb = bootstrapScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await ApplyMigrationsAsync(bootstrapDb);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null) await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Happy path: mixed-status seed → only eligible Sent rows deleted
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_DeletesOnly_SentRowsOlderThanRetention()
    {
        // Arrange: 4 categories of rows around the 30-day retention cutoff.
        var cutoff = FixedNow.AddDays(-30);
        var sentOldEnqueuedAt = cutoff.AddDays(-5);     // 35 days old → eligible
        var sentOldDispatchedAt = cutoff.AddDays(-5);   // 35 days old → eligible
        var sentFreshEnqueuedAt = cutoff.AddDays(2);    // 28 days old → KEEP
        var sentFreshDispatchedAt = cutoff.AddDays(2);  // 28 days old → KEEP
        var failedOldEnqueuedAt = cutoff.AddDays(-10);  // 40 days old, but Failed → KEEP
        var pendingOldEnqueuedAt = cutoff.AddDays(-10); // 40 days old, but Pending → KEEP

        var sentOldIds = new List<Guid>();
        var sentFreshIds = new List<Guid>();
        Guid failedOldId;
        Guid pendingOldId;

        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            // 5 Sent-old rows → eligible for purge
            for (var i = 0; i < 5; i++)
            {
                var row = SeedRow(sentOldEnqueuedAt.AddSeconds(i), status: DomainEventOutboxStatus.Sent,
                    dispatchedAt: sentOldDispatchedAt.AddSeconds(i));
                db.DomainEventOutbox.Add(row);
                sentOldIds.Add(row.Id);
            }

            // 3 Sent-fresh rows → keep
            for (var i = 0; i < 3; i++)
            {
                var row = SeedRow(sentFreshEnqueuedAt.AddSeconds(i), status: DomainEventOutboxStatus.Sent,
                    dispatchedAt: sentFreshDispatchedAt.AddSeconds(i));
                db.DomainEventOutbox.Add(row);
                sentFreshIds.Add(row.Id);
            }

            // 1 Failed-old row → keep (operator must triage)
            var failedRow = SeedRow(failedOldEnqueuedAt, status: DomainEventOutboxStatus.Failed,
                dispatchedAt: null);
            db.DomainEventOutbox.Add(failedRow);
            failedOldId = failedRow.Id;

            // 1 Pending-old row → keep (the processor will dispatch it eventually)
            var pendingRow = SeedRow(pendingOldEnqueuedAt, status: DomainEventOutboxStatus.Pending,
                dispatchedAt: null);
            db.DomainEventOutbox.Add(pendingRow);
            pendingOldId = pendingRow.Id;

            await db.SaveChangesAsync(TestCancellationToken);
        }

        // Act
        var service = _serviceProvider!.GetRequiredService<DomainEventOutboxRetentionService>();
        var purgedCount = await service.RunOnceAsync(TestCancellationToken);

        // Assert
        purgedCount.Should().Be(5, because: "5 Sent-old rows were eligible for purge");

        await using (var verifyScope = _serviceProvider!.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var surviving = await db.DomainEventOutbox.AsNoTracking().ToListAsync(TestCancellationToken);

            surviving.Should().HaveCount(5,
                because: "3 Sent-fresh + 1 Failed-old + 1 Pending-old survive (8 seeded - 5 purged = 3 + 1 + 1 = 5)");

            surviving.Where(r => sentOldIds.Contains(r.Id)).Should().BeEmpty(
                "the eligible Sent-old rows must all be deleted");
            surviving.Where(r => sentFreshIds.Contains(r.Id)).Should().HaveCount(3,
                "Sent rows inside the retention window survive");
            surviving.Should().Contain(r => r.Id == failedOldId,
                "Failed rows are NEVER auto-deleted regardless of age — operator triage required");
            surviving.Should().Contain(r => r.Id == pendingOldId,
                "Pending rows survive regardless of age — the processor owns their lifecycle");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Empty queue: no eligible rows → no-op
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_EmptyQueue_ReturnsZero()
    {
        var service = _serviceProvider!.GetRequiredService<DomainEventOutboxRetentionService>();
        var purgedCount = await service.RunOnceAsync(TestCancellationToken);

        purgedCount.Should().Be(0,
            because: "no rows seeded → nothing to purge");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Chunked drain: larger-than-batch eligible set → multiple chunks, full drain
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_LargeEligibleSet_DrainsAllChunks()
    {
        // Override the batch size for a quick assertion that the service loops through
        // chunks until the eligible set is empty.
        await using var smallChunkScope = _serviceProvider!.CreateAsyncScope();
        var smallChunkService = new DomainEventOutboxRetentionService(
            smallChunkScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            smallChunkScope.ServiceProvider.GetRequiredService<ILogger<DomainEventOutboxRetentionService>>(),
            Options.Create(new DomainEventOutboxOptions
            {
                SentRetentionDays = 30,
                RetentionIntervalHours = 1,
                RetentionBatchSize = 3,   // smaller than eligible set → forces multiple chunks
            }),
            _timeProvider!);

        var oldDispatchedAt = FixedNow.AddDays(-35);

        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            for (var i = 0; i < 10; i++)
            {
                var row = SeedRow(oldDispatchedAt.AddSeconds(i), status: DomainEventOutboxStatus.Sent,
                    dispatchedAt: oldDispatchedAt.AddSeconds(i));
                db.DomainEventOutbox.Add(row);
            }
            await db.SaveChangesAsync(TestCancellationToken);
        }

        var purgedCount = await smallChunkService.RunOnceAsync(TestCancellationToken);

        purgedCount.Should().Be(10,
            because: "the service must loop through chunks of 3 until the eligible set is empty " +
            "(chunks: 3 + 3 + 3 + 1 = 10)");

        await using var verifyScope = _serviceProvider!.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var remaining = await verifyDb.DomainEventOutbox.AsNoTracking().CountAsync(TestCancellationToken);
        remaining.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task ApplyMigrationsAsync(MeepleAiDbContext db)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }
        }
    }

    /// <summary>
    /// Constructs an outbox row in the requested state. EnqueueOutboxRows is bypassed — we
    /// drive the entity through its public factory + transitions to land it at the
    /// target state without firing the SaveChangesAsync routing pipeline.
    /// </summary>
    private static DomainEventOutboxEntity SeedRow(
        DateTimeOffset enqueuedAt,
        DomainEventOutboxStatus status,
        DateTimeOffset? dispatchedAt)
    {
        var fakeEvent = new SeedFakeEvent();
        var row = DomainEventOutboxEntity.Enqueue(
            fakeEvent,
            FakeEventAlias,
            payloadJson: """{"marker":"seed"}""",
            payloadVersion: 1,
            correlationId: null,
            now: enqueuedAt);

        switch (status)
        {
            case DomainEventOutboxStatus.Pending:
                break;
            case DomainEventOutboxStatus.Sent:
                row.MarkSent(dispatchedAt ?? enqueuedAt);
                break;
            case DomainEventOutboxStatus.Failed:
                row.MarkFailed("seeded terminal failure", enqueuedAt);
                break;
        }

        return row;
    }

    /// <summary>Minimal <see cref="IDomainEvent"/> stub.</summary>
    private sealed class SeedFakeEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
