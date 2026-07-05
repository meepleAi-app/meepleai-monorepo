using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Unit tests for <see cref="ProcessingQueueMonitorService"/>.
/// Issue #2689: Validates that the monitor sends <see cref="DegradeStuckJobCommand"/>
/// when a job is stuck past the recovery threshold (default 30 min), and does NOT send it
/// when the job is only past the alert threshold (10 min) but below the recovery threshold.
/// The early SSE alert (AlertDocumentStuck) must remain unchanged — degrade is ADDITIVE.
/// Tests call <c>RunChecksAsync</c> directly, bypassing the 2-minute loop delay.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class ProcessingQueueMonitorServiceTests
{
    // ──────────────────────────────────────────────────────────────────
    // Helpers
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

    private static ProcessingQueueMonitorService BuildMonitor(IServiceScopeFactory scopeFactory)
    {
        // Empty configuration → all thresholds use their defaults:
        //   StuckJobTimeout          = 10 min
        //   StuckJobRecoveryTimeout  = 30 min
        var configuration = new ConfigurationBuilder().Build();
        return new ProcessingQueueMonitorService(
            scopeFactory,
            configuration,
            NullLogger<ProcessingQueueMonitorService>.Instance);
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 1: job stuck 40 min → DegradeStuckJobCommand sent (past recovery threshold)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckStuckJobs_JobStuckPastRecoveryThreshold_SendsDegradeCommand()
    {
        // Arrange: job started 40 min ago (> 30-min recovery threshold)
        var dbName = $"monitor_degrade_{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-40);
        var jobId = await SeedStuckJobAsync(dbName, startedAt);

        var (scopeFactory, mediatorMock, _) = BuildScopeFactory(dbName);
        var monitor = BuildMonitor(scopeFactory);

        // Act — call the internal method directly, bypassing the 2-minute loop delay
        await monitor.RunChecksAsync(CancellationToken.None);

        // Assert: DegradeStuckJobCommand was sent exactly once for the stuck job
        mediatorMock.Verify(
            m => m.Send(
                It.Is<DegradeStuckJobCommand>(c => c.JobId == jobId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 2: job stuck 15 min → SSE alert fires but NO DegradeStuckJobCommand
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckStuckJobs_JobBelowRecoveryThreshold_PublishesAlertButDoesNotSendDegradeCommand()
    {
        // Arrange: job started 15 min ago (> 10-min alert threshold, < 30-min recovery threshold)
        var dbName = $"monitor_alert_only_{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
        await SeedStuckJobAsync(dbName, startedAt);

        var (scopeFactory, mediatorMock, streamMock) = BuildScopeFactory(dbName);
        var monitor = BuildMonitor(scopeFactory);

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
}
