using System.Diagnostics.Metrics;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;
using Api.BoundedContexts.Administration.Infrastructure.Health;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Observability;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for T4b idempotency + health-tracker gauges.
///
/// Scenarios:
///   1. <c>Processor_DoesNotDuplicate_OnRetryAfterPartialBatch</c>
///        Seed 5 Pending rows → drain → 5 audit_logs. Re-flag 2 outbox rows back to Pending
///        and drain again. audit_logs MUST still be 5 (no duplicates), and the 2 re-drained
///        rows transition back to Sent.
///
///   2. <c>HealthTracker_ReflectsPendingCount_AfterDrain</c>
///        Seed 1 Pending row → drain. Tracker MUST report PendingCount = 0 and
///        oldest_pending_age_seconds = 0.
///
///   3. <c>RegisterAuditOutboxGauges_PublishesObservableInstruments</c>
///        Verifies that the gauges defined in <c>MeepleAiMetrics.AuditOutbox</c> emit
///        the snapshot recorded by the tracker via the MeterListener API.
///
/// SP5 Admin Security S1 — Task 4b.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class AuditOutboxIdempotencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AuditOutboxIdempotencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_audit_outbox_idempotency_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddSingleton<IAuditOutboxHealthTracker, AuditOutboxHealthTracker>();
        services.AddSingleton<AuditOutboxProcessor>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await ApplyMigrationsAsync(_dbContext);
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
    // Idempotency under retry (Q3 primary contract)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Processor_DoesNotDuplicate_OnRetryAfterPartialBatch()
    {
        // Arrange: 5 Pending rows. Each carries a deterministic Guid that will become the
        // audit_logs.Id — this is the basis for T4b idempotency.
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var processor = _serviceProvider!.GetRequiredService<AuditOutboxProcessor>();

        var now = DateTimeOffset.UtcNow;
        var auditIds = new List<Guid>(capacity: 5);
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            auditIds.Add(id);
            db.AuditOutbox.Add(AuditOutboxEntity.CreatePending(
                id,
                JsonSerializer.Serialize(BuildPayload($"Idempotency.{i}", now.AddSeconds(i))),
                now.AddSeconds(i)));
        }
        await db.SaveChangesAsync(TestCancellationToken);

        // Act 1: first drain — all 5 rows materialize cleanly.
        var processed1 = await processor.RunOnceAsync(100, TestCancellationToken);

        processed1.Should().Be(5);
        db.ChangeTracker.Clear();
        (await db.AuditLogs.AsNoTracking().CountAsync(TestCancellationToken))
            .Should().Be(5, because: "first drain materializes 5 audit_logs rows");

        // Arrange (retry simulation): re-flag the first 2 outbox rows back to Pending via the
        // internal test helper on AuditOutboxEntity. The audit_logs rows for these ids still exist
        // — this is exactly the scenario the idempotency pre-check must catch.
        var firstTwoIds = auditIds.Take(2).ToList();
        var firstTwoRows = await db.AuditOutbox
            .Where(r => firstTwoIds.Contains(r.Id))
            .ToListAsync(TestCancellationToken);
        foreach (var row in firstTwoRows)
        {
            row.MarkPendingForTest();
        }
        await db.SaveChangesAsync(TestCancellationToken);

        // Act 2: second drain — the 2 re-flagged rows are seen as Pending again.
        var processed2 = await processor.RunOnceAsync(100, TestCancellationToken);

        // Assert: the second drain processes the 2 rows, but audit_logs cardinality is unchanged
        // (no duplicate inserts) and the rows transition back to Sent.
        processed2.Should().Be(2);

        db.ChangeTracker.Clear();
        (await db.AuditLogs.AsNoTracking().CountAsync(TestCancellationToken))
            .Should().Be(5, because: "T4b: audit_logs must NOT grow on retry — idempotency by Id");

        (await db.AuditOutbox.AsNoTracking()
            .CountAsync(r => r.Status == OutboxStatus.Sent, TestCancellationToken))
            .Should().Be(5, because: "all 5 outbox rows must be Sent after the retry drain");

        (await db.AuditOutbox.AsNoTracking()
            .CountAsync(r => r.Status == OutboxStatus.Pending, TestCancellationToken))
            .Should().Be(0, because: "no rows must remain Pending after the second drain");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Health tracker snapshot
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthTracker_ReflectsPendingCount_AfterDrain()
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var processor = _serviceProvider!.GetRequiredService<AuditOutboxProcessor>();
        var tracker = _serviceProvider!.GetRequiredService<IAuditOutboxHealthTracker>();

        var now = DateTimeOffset.UtcNow;
        db.AuditOutbox.Add(AuditOutboxEntity.CreatePending(
            Guid.NewGuid(),
            JsonSerializer.Serialize(BuildPayload("Health.1", now)),
            now));
        await db.SaveChangesAsync(TestCancellationToken);

        // Pre-drain assertion is intentionally omitted: snapshots are populated by the
        // processor, not by seeding. Drive a drain pass and assert the post-state.
        await processor.RunOnceAsync(100, TestCancellationToken);

        tracker.GetPendingCount().Should().Be(0,
            because: "the single Pending row was drained — tracker must report 0");
        tracker.GetOldestPendingAgeSeconds().Should().Be(0,
            because: "no Pending rows means age is 0 (sentinel for an empty queue)");
        tracker.GetFailedCount().Should().Be(0,
            because: "no payload errors occurred — Failed bucket stays empty");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // ObservableGauge wiring
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RegisterAuditOutboxGauges_PublishesObservableInstruments()
    {
        // Pre-populate the tracker so the gauge callbacks see deterministic values.
        var tracker = new AuditOutboxHealthTracker();
        tracker.RecordSnapshot(pendingCount: 7, oldestPendingAgeSeconds: 42.5, failedCount: 3);

        // The static registration flag is process-global. Reset before/after to keep this
        // test independent of the registration order across the suite.
        MeepleAiMetrics.ResetAuditOutboxGaugesForTest();
        try
        {
            var observed = new Dictionary<string, double>(StringComparer.Ordinal);
            using var listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name.StartsWith("meepleai.audit.outbox.", StringComparison.Ordinal))
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            listener.SetMeasurementEventCallback<long>(
                (inst, value, _, _) => observed[inst.Name] = value);
            listener.SetMeasurementEventCallback<double>(
                (inst, value, _, _) => observed[inst.Name] = value);
            listener.Start();

            MeepleAiMetrics.RegisterAuditOutboxGauges(tracker);
            listener.RecordObservableInstruments();

            observed.Should().ContainKey("meepleai.audit.outbox.pending.count")
                .WhoseValue.Should().Be(7);
            observed.Should().ContainKey("meepleai.audit.outbox.pending.oldest_age_seconds")
                .WhoseValue.Should().Be(42.5);
            observed.Should().ContainKey("meepleai.audit.outbox.failed.count")
                .WhoseValue.Should().Be(3);
        }
        finally
        {
            MeepleAiMetrics.ResetAuditOutboxGaugesForTest();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    private static AuditOutboxPayload BuildPayload(string action, DateTimeOffset timestamp)
        => new()
        {
            Action = action,
            Resource = "TestResource",
            UserId = Guid.NewGuid().ToString(),
            ResourceId = "test-resource-id",
            Result = "Success",
            IpAddress = "127.0.0.1",
            UserAgent = "xunit",
            RequestType = "TestCommand",
            Details = "{}",
            Timestamp = timestamp,
            Oversize = false,
        };

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
}
