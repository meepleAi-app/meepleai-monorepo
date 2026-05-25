using System.Text.Json;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for <see cref="AuditOutboxProcessor"/> (SP5 Admin Security S1 — Task 4).
///
/// TDD-first scaffold: this test asserts the documented drain semantics. The processor
/// implementation is currently a stub that throws <c>NotImplementedException</c>, so this
/// test MUST fail on the initial commit. Step 3 implements the loop and the test flips green.
///
/// Drain contract under test (Plan T4 step 1):
///   GIVEN 3 Pending rows in audit_outbox AND audit_logs is empty
///   WHEN  AuditOutboxProcessor.RunOnceAsync(batchSize: 100) is invoked
///   THEN  it returns 3
///   AND   audit_logs contains 3 rows
///   AND   all 3 source outbox rows are marked Sent
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class AuditOutboxProcessorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AuditOutboxProcessorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_audit_outbox_processor_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Register the processor as a singleton — same shape as production DI in
        // ApplicationServiceExtensions.AddAuditAndLogging. The hosted-service wrapper is NOT
        // registered here: we drive the drain explicitly via RunOnceAsync to keep the test
        // deterministic (no dependency on the 5-second poll interval).
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
    // Drain semantics: 3 Pending rows → 3 audit_logs rows + 3 Sent outbox rows
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Processor_DrainsPendingRows_To_AuditLogs_AndMarksSent()
    {
        // Arrange: seed 3 Pending outbox rows with realistic AuditOutboxPayload-shaped JSON.
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
        {
            var payload = new AuditOutboxPayload
            {
                Action = $"TestAction.{i}",
                Resource = "TestResource",
                UserId = Guid.NewGuid().ToString(),
                ResourceId = $"resource-{i}",
                Result = "Success",
                IpAddress = "127.0.0.1",
                UserAgent = "xunit",
                RequestType = "TestCommand",
                Details = "{}",
                Timestamp = now.AddSeconds(i),
                Oversize = false,
            };
            db.AuditOutbox.Add(AuditOutboxEntity.CreatePending(
                JsonSerializer.Serialize(payload),
                now.AddSeconds(i)));
        }
        await db.SaveChangesAsync(TestCancellationToken);

        // Act: drive a single drain pass — batchSize >= 3 so all rows fit.
        var processor = _serviceProvider!.GetRequiredService<AuditOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 100, cancellationToken: TestCancellationToken);

        // Assert: per-batch count, audit_logs cardinality, and outbox state transition.
        processed.Should().Be(3,
            because: "RunOnceAsync returns the number of rows processed in this batch");

        db.ChangeTracker.Clear();
        var auditLogCount = await db.AuditLogs.AsNoTracking().CountAsync(TestCancellationToken);
        auditLogCount.Should().Be(3,
            because: "each Pending outbox row must materialize exactly one audit_logs row");

        var sentCount = await db.AuditOutbox.AsNoTracking()
            .CountAsync(r => r.Status == OutboxStatus.Sent, TestCancellationToken);
        sentCount.Should().Be(3,
            because: "all drained rows must transition Pending → Sent on success");

        var pendingCount = await db.AuditOutbox.AsNoTracking()
            .CountAsync(r => r.Status == OutboxStatus.Pending, TestCancellationToken);
        pendingCount.Should().Be(0,
            because: "no rows must remain Pending after a successful drain pass");
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
}
