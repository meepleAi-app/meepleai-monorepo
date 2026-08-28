using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Unit tests for StalePdfRecoveryService.RecoverAllAsync.
/// Issue #2689: Validates that the log summary reflects the real outcome
/// (re-read from DB after ProcessAsync), not an unconditional "recovered".
/// Tests bypass ExecuteAsync (which awaits 30 s) by calling RecoverAllAsync directly.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class StalePdfRecoveryServiceTests
{
    // ──────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a real IServiceScopeFactory backed by an in-memory DbContext
    /// (shared via database name) and the supplied pipeline mock.
    /// </summary>
    private static IServiceScopeFactory BuildScopeFactory(
        string dbName,
        IPdfProcessingPipelineService pipeline)
    {
        var services = new ServiceCollection();
        // Each scope resolves a fresh DbContext instance backed by the same in-memory store.
        services.AddScoped<MeepleAiDbContext>(_ => TestDbContextFactory.CreateInMemoryDbContext(dbName));
        services.AddScoped<IPdfProcessingPipelineService>(_ => pipeline);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>Seeds one stale PdfDocumentEntity in Embedding state.</summary>
    private static async Task<Guid> SeedStalePdfAsync(string dbName)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var id = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = "rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            // Old enough to be stale (ProcessingStaleness = 30 min)
            UploadedAt = DateTime.UtcNow.AddHours(-2),
            ProcessingState = "Embedding",
        });
        await db.SaveChangesAsync();
        return id;
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 1 (RED first): PDF does not progress past Pending after reset → stillStuck
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecoverAll_WhenPdfDoesNotProgressAfterReset_CountsAsStillStuck()
    {
        // Arrange
        var dbName = $"stale_stuck_{Guid.NewGuid():N}";
        await SeedStalePdfAsync(dbName);

        // Pipeline mock does nothing → PDF stays Pending (after ResetToPendingAsync resets it)
        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        pipelineMock
            .Setup(p => p.ProcessAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PdfPipelineOutcome.Processed);

        var scopeFactory = BuildScopeFactory(dbName, pipelineMock.Object);
        var sut = new StalePdfRecoveryService(
            scopeFactory,
            NullLogger<StalePdfRecoveryService>.Instance);

        // Act — call the extracted loop directly, bypassing the 30 s startup delay
        var (recovered, failed, stillStuck) = await sut.RecoverAllAsync(CancellationToken.None);

        // Assert: not recovered, not failed, counted as still stuck
        Assert.Equal(0, recovered);
        Assert.Equal(0, failed);
        Assert.Equal(1, stillStuck);
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 2: PDF reaches Ready → recovered
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecoverAll_WhenPdfReachesReady_CountsAsRecovered()
    {
        // Arrange
        var dbName = $"stale_ready_{Guid.NewGuid():N}";
        var pdfId = await SeedStalePdfAsync(dbName);

        // Pipeline mock simulates successful processing: sets state = Ready in the shared DB.
        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        pipelineMock
            .Setup(p => p.ProcessAsync(
                pdfId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, string fp, Guid userId, CancellationToken ct) =>
            {
                // Write Ready state into the shared in-memory DB.
                using var db = TestDbContextFactory.CreateInMemoryDbContext(dbName);
                // #3866: FindAsync follows QueryTrackingBehavior, and the context now defaults to
                // NoTracking like production — without AsTracking this simulated write is a no-op.
                var entity = await db.PdfDocuments
                    .AsTracking()
                    .FirstOrDefaultAsync(e => e.Id == id, ct);
                if (entity != null)
                {
                    entity.ProcessingState = "Ready";
                    await db.SaveChangesAsync(ct);
                }
                return PdfPipelineOutcome.Processed;
            });

        var scopeFactory = BuildScopeFactory(dbName, pipelineMock.Object);
        var sut = new StalePdfRecoveryService(
            scopeFactory,
            NullLogger<StalePdfRecoveryService>.Instance);

        // Act
        var (recovered, failed, stillStuck) = await sut.RecoverAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(0, failed);
        Assert.Equal(0, stillStuck);
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 3: PDF ends in Failed state → failed branch (lines 85-91 in prod)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecoverAll_WhenPdfEndsInFailed_CountsAsFailed()
    {
        // Arrange
        var dbName = $"stale_failed_{Guid.NewGuid():N}";
        var pdfId = await SeedStalePdfAsync(dbName);

        // Pipeline mock simulates a failed processing run: writes state = Failed into the shared DB.
        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        pipelineMock
            .Setup(p => p.ProcessAsync(
                pdfId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, string fp, Guid userId, CancellationToken ct) =>
            {
                using var db = TestDbContextFactory.CreateInMemoryDbContext(dbName);
                // #3866: FindAsync follows QueryTrackingBehavior, and the context now defaults to
                // NoTracking like production — without AsTracking this simulated write is a no-op.
                var entity = await db.PdfDocuments
                    .AsTracking()
                    .FirstOrDefaultAsync(e => e.Id == id, ct);
                if (entity != null)
                {
                    entity.ProcessingState = "Failed";
                    await db.SaveChangesAsync(ct);
                }
                return PdfPipelineOutcome.Failed;
            });

        var scopeFactory = BuildScopeFactory(dbName, pipelineMock.Object);
        var sut = new StalePdfRecoveryService(
            scopeFactory,
            NullLogger<StalePdfRecoveryService>.Instance);

        // Act
        var (recovered, failed, stillStuck) = await sut.RecoverAllAsync(CancellationToken.None);

        // Assert: the finalState == "Failed" branch increments failed, not recovered/stillStuck
        Assert.Equal(0, recovered);
        Assert.Equal(1, failed);
        Assert.Equal(0, stillStuck);
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 4: ProcessAsync throws → catch block increments failed (lines 106-110 in prod)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecoverAll_WhenProcessAsyncThrows_CountsAsFailed()
    {
        // Arrange
        var dbName = $"stale_throws_{Guid.NewGuid():N}";
        await SeedStalePdfAsync(dbName);

        // Pipeline mock throws: the catch block at the bottom of the recovery loop
        // increments failed++ and logs the error, rather than bubbling the exception.
        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        pipelineMock
            .Setup(p => p.ProcessAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated pipeline failure"));

        var scopeFactory = BuildScopeFactory(dbName, pipelineMock.Object);
        var sut = new StalePdfRecoveryService(
            scopeFactory,
            NullLogger<StalePdfRecoveryService>.Instance);

        // Act
        var (recovered, failed, stillStuck) = await sut.RecoverAllAsync(CancellationToken.None);

        // Assert: exception path → failed++, no recovered or stillStuck
        Assert.Equal(0, recovered);
        Assert.Equal(1, failed);
        Assert.Equal(0, stillStuck);
    }
}
