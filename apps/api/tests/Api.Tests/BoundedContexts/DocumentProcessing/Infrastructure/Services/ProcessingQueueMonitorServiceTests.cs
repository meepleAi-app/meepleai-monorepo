using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Tests for ProcessingQueueMonitorService constructor validation and behavior.
/// Issue #5460: Proactive alerts background service.
/// Issue #2689: Validates that the monitor sends <see cref="DegradeStuckJobCommand"/>
/// when a job is stuck past the recovery threshold (default 30 min), and does NOT send it
/// when the job is only past the alert threshold (10 min) but below the recovery threshold.
/// Issue #2693: Validates that auto-degrade is suppressed within the startup grace period
/// (default 15 min) to avoid racing StalePdfRecoveryService on the first monitor cycle.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class ProcessingQueueMonitorServiceTests
{
    // ──────────────────────────────────────────────────────────────────
    // Instance fields used by constructor-guard tests
    // ──────────────────────────────────────────────────────────────────

    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProcessingQueueMonitor:CheckIntervalSeconds"] = "120",
        })
        .Build();
    private readonly Mock<ILogger<ProcessingQueueMonitorService>> _loggerMock = new();

    // ──────────────────────────────────────────────────────────────────
    // Constructor-guard tests (3)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ProcessingQueueMonitorService(
            null!,
            _configuration,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("scopeFactory");
    }

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ProcessingQueueMonitorService(
            _scopeFactoryMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ProcessingQueueMonitorService(
            _scopeFactoryMock.Object,
            _configuration,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    // ──────────────────────────────────────────────────────────────────
    // Behavior-test helpers
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="IServiceScopeFactory"/> backed by an in-memory DbContext
    /// (shared via database name) and supplied mock services.
    /// </summary>
    private static (IServiceScopeFactory ScopeFactory, Mock<IMediator> MediatorMock, Mock<IQueueStreamService> StreamMock)
        BuildScopeFactory(string dbName)
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<DegradeStuckJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DegradeStuckJobResult(true, "Degraded to Failed"));

        var streamMock = new Mock<IQueueStreamService>();
        streamMock
            .Setup(s => s.PublishQueueEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        // Each scope resolves a fresh DbContext instance backed by the same in-memory store.
        services.AddScoped<MeepleAiDbContext>(_ => TestDbContextFactory.CreateInMemoryDbContext(dbName));
        services.AddScoped<IQueueStreamService>(_ => streamMock.Object);
        services.AddScoped<IMediator>(_ => mediatorMock.Object);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return (scopeFactory, mediatorMock, streamMock);
    }

    /// <summary>
    /// Seeds a <see cref="PdfDocumentEntity"/> + a stuck <see cref="ProcessingJobEntity"/>
    /// into the shared in-memory store. Returns the seeded job id.
    /// </summary>
    private static async Task<Guid> SeedStuckJobAsync(string dbName, DateTimeOffset startedAt)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(dbName);

        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow.AddHours(-2),
            ProcessingState = "Embedding",
        });

        var jobId = Guid.NewGuid();
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfId,
            UserId = Guid.NewGuid(),
            Status = "Processing",
            StartedAt = startedAt,
            CreatedAt = startedAt.AddMinutes(-5),
        });

        await db.SaveChangesAsync();
        return jobId;
    }

    /// <summary>
    /// Builds a <see cref="ProcessingQueueMonitorService"/> using empty configuration
    /// (all thresholds default: StuckJobTimeout=10min, StuckJobRecoveryTimeout=30min,
    /// StartupGracePeriodMinutes=15min) and an optional <paramref name="timeProvider"/>.
    /// The <paramref name="timeProvider"/> clock is sampled in the constructor to capture
    /// <c>_serviceStartedAt</c>, so advance it AFTER this call to simulate elapsed time.
    /// </summary>
    private static ProcessingQueueMonitorService BuildMonitor(
        IServiceScopeFactory scopeFactory,
        TimeProvider? timeProvider = null)
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ProcessingQueueMonitorService(
            scopeFactory,
            configuration,
            NullLogger<ProcessingQueueMonitorService>.Instance,
            timeProvider);
    }

    // ──────────────────────────────────────────────────────────────────
    // Behavior test 1: job stuck 40 min → DegradeStuckJobCommand sent
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckStuckJobs_JobStuckPastRecoveryThreshold_SendsDegradeCommand()
    {
        // Arrange: job started 40 min ago (> 30-min recovery threshold)
        var dbName = $"monitor_degrade_{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-40);
        var jobId = await SeedStuckJobAsync(dbName, startedAt);

        var (scopeFactory, mediatorMock, _) = BuildScopeFactory(dbName);

        // Construct first (captures _serviceStartedAt = T), then advance past the 15-min
        // startup grace window so the degrade gate is open (Issue #2693 regression fix).
        var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var monitor = BuildMonitor(scopeFactory, tp);
        tp.Advance(TimeSpan.FromMinutes(31));

        // Act — call the internal method directly, bypassing the 2-minute loop delay
        await monitor.RunChecksAsync(CancellationToken.None);

        // Assert: DegradeStuckJobCommand was sent exactly once for the stuck job,
        // with StuckMinutes >= 30 (lower-bound guard against wrong-units regressions).
        mediatorMock.Verify(
            m => m.Send(
                It.Is<DegradeStuckJobCommand>(c => c.JobId == jobId && c.StuckMinutes >= 30),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────
    // Behavior test 2: job stuck 15 min → SSE alert fires but NO degrade
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckStuckJobs_JobBelowRecoveryThreshold_PublishesAlertButDoesNotSendDegradeCommand()
    {
        // Arrange: job started 15 min ago (> 10-min alert threshold, < 30-min recovery threshold)
        var dbName = $"monitor_alert_only_{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
        await SeedStuckJobAsync(dbName, startedAt);

        var (scopeFactory, mediatorMock, streamMock) = BuildScopeFactory(dbName);

        // Construct first (captures _serviceStartedAt = T), then advance past the 15-min
        // startup grace window so the degrade gate is open (Issue #2693 regression fix).
        var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var monitor = BuildMonitor(scopeFactory, tp);
        tp.Advance(TimeSpan.FromMinutes(31));

        // Act
        await monitor.RunChecksAsync(CancellationToken.None);

        // Assert: early SSE alert was still published (stuck detection unchanged)
        streamMock.Verify(
            s => s.PublishQueueEventAsync(
                It.Is<QueueStreamEvent>(e => e.Type == QueueStreamEventType.AlertDocumentStuck),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: DegradeStuckJobCommand was NOT sent (job is below the 30-min recovery threshold)
        mediatorMock.Verify(
            m => m.Send(It.IsAny<DegradeStuckJobCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // Grace-period test A: within grace → SSE alert fires but NO degrade
    // Issue #2693
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckStuckJobs_WithinStartupGracePeriod_PublishesAlertButDoesNotDegrade()
    {
        // Arrange: job started 40 min ago (past both the 10-min alert and 30-min recovery
        // thresholds), but the service is brand-new — grace not yet elapsed.
        var dbName = $"monitor_grace_suppress_{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-40);
        var jobId = await SeedStuckJobAsync(dbName, startedAt);

        var (scopeFactory, mediatorMock, streamMock) = BuildScopeFactory(dbName);

        // FakeTimeProvider is NOT advanced after construction:
        // elapsed = 0 min < 15 min default grace → degrade suppressed.
        var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var monitor = BuildMonitor(scopeFactory, tp);
        // No tp.Advance() — service just started.

        // Act
        await monitor.RunChecksAsync(CancellationToken.None);

        // Assert: SSE alert still fires (stuck detection is unaffected by grace)
        streamMock.Verify(
            s => s.PublishQueueEventAsync(
                It.Is<QueueStreamEvent>(e => e.Type == QueueStreamEventType.AlertDocumentStuck),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: degrade suppressed — grace not yet elapsed
        mediatorMock.Verify(
            m => m.Send(It.IsAny<DegradeStuckJobCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // Grace-period test B: after grace → degrade fires
    // Issue #2693
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckStuckJobs_AfterStartupGracePeriod_SendsDegradeCommand()
    {
        // Arrange: job started 40 min ago (> 30-min recovery threshold)
        // and the service clock is advanced beyond the 15-min grace window.
        var dbName = $"monitor_grace_degrade_{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-40);
        var jobId = await SeedStuckJobAsync(dbName, startedAt);

        var (scopeFactory, mediatorMock, _) = BuildScopeFactory(dbName);

        // Construct first (captures _serviceStartedAt = T), then advance 16 min
        // (> 15 min default grace) so the degrade gate opens.
        var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var monitor = BuildMonitor(scopeFactory, tp);
        tp.Advance(TimeSpan.FromMinutes(16));

        // Act
        await monitor.RunChecksAsync(CancellationToken.None);

        // Assert: degrade fires once with correct job id and stuck minutes
        mediatorMock.Verify(
            m => m.Send(
                It.Is<DegradeStuckJobCommand>(c => c.JobId == jobId && c.StuckMinutes >= 30),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
