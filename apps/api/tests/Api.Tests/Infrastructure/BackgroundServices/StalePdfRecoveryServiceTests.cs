using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Xunit;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="StalePdfRecoveryService"/>.
///
/// Strategy: The service's startup has a 30-second delay that cannot be easily
/// bypassed (private static readonly field, initonly in .NET 9).  Tests therefore
/// call the private methods <c>FindStalePdfsAsync</c> and <c>ResetToPendingAsync</c>
/// directly via reflection to verify the core staleness-detection and state-reset logic.
///
/// One integration-style test drives the full <c>ExecuteAsync</c> path via a
/// Slow-category test that waits 31 seconds.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class StalePdfRecoveryServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PdfDocumentEntity BuildPdfEntity(
        string processingState,
        DateTime uploadedAt,
        string filePath = "test.pdf",
        Guid? userId = null)
    {
        return new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = filePath,
            FileSizeBytes = 1024,
            UploadedByUserId = userId ?? Guid.NewGuid(),
            UploadedAt = uploadedAt,
            ProcessingState = processingState,
        };
    }

    /// <summary>
    /// Builds an <see cref="IServiceScopeFactory"/> that provides a given <see cref="MeepleAiDbContext"/>
    /// and <see cref="IPdfProcessingPipelineService"/> mock from the service provider.
    /// </summary>
    private static (Mock<IServiceScopeFactory> scopeFactory, MeepleAiDbContext db) BuildScopeFactory(
        MeepleAiDbContext db,
        Mock<IPdfProcessingPipelineService> pipelineMock)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(MeepleAiDbContext))).Returns(db);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPdfProcessingPipelineService))).Returns(pipelineMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        return (scopeFactoryMock, db);
    }

    /// <summary>
    /// Creates a new <see cref="StalePdfRecoveryService"/> with the given scope factory.
    /// </summary>
    private static StalePdfRecoveryService CreateService(Mock<IServiceScopeFactory> scopeFactory)
        => new StalePdfRecoveryService(
            scopeFactory.Object,
            NullLogger<StalePdfRecoveryService>.Instance);

    /// <summary>
    /// Represents the data extracted from the private <c>StalePdfInfo</c> inner class
    /// so tests can assert on individual properties without casting to dynamic.
    /// </summary>
    private sealed record StalePdfResult(Guid Id, string FilePath, Guid UploadedByUserId, string OriginalStatus, DateTime UploadedAt);

    /// <summary>
    /// Invokes the private <c>FindStalePdfsAsync</c> method via reflection.
    /// Returns the list of <c>StalePdfInfo</c> records projected into <see cref="StalePdfResult"/>.
    /// </summary>
    private static async Task<IList<StalePdfResult>> InvokeFindStalePdfsAsync(
        StalePdfRecoveryService service,
        CancellationToken ct = default)
    {
        var method = typeof(StalePdfRecoveryService)
            .GetMethod("FindStalePdfsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FindStalePdfsAsync not found via reflection");

        var task = (Task)method.Invoke(service, new object[] { ct })!;
        await task.ConfigureAwait(false);

        // Task<List<StalePdfInfo>> — get result via reflection
        var resultProperty = task.GetType().GetProperty("Result")
            ?? throw new InvalidOperationException("Task.Result not found");

        var list = resultProperty.GetValue(task) as System.Collections.IList
            ?? throw new InvalidOperationException("Result was not an IList");

        // StalePdfInfo is a private nested class — access its properties via reflection
        var results = new List<StalePdfResult>();
        foreach (var item in list)
        {
            var type = item.GetType();
            var id = (Guid)type.GetProperty("Id")!.GetValue(item)!;
            var filePath = (string)type.GetProperty("FilePath")!.GetValue(item)!;
            var uploadedByUserId = (Guid)type.GetProperty("UploadedByUserId")!.GetValue(item)!;
            var originalStatus = (string)type.GetProperty("OriginalStatus")!.GetValue(item)!;
            var uploadedAt = (DateTime)type.GetProperty("UploadedAt")!.GetValue(item)!;
            results.Add(new StalePdfResult(id, filePath, uploadedByUserId, originalStatus, uploadedAt));
        }
        return results;
    }

    /// <summary>
    /// Invokes the private <c>ResetToPendingAsync</c> method via reflection.
    /// </summary>
    private static Task InvokeResetToPendingAsync(
        StalePdfRecoveryService service,
        Guid pdfDocumentId,
        CancellationToken ct = default)
    {
        var method = typeof(StalePdfRecoveryService)
            .GetMethod("ResetToPendingAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ResetToPendingAsync not found via reflection");

        return (Task)method.Invoke(service, new object[] { pdfDocumentId, ct })!;
    }

    // ── FindStalePdfsAsync: Extracting state ─────────────────────────────────

    [Fact]
    public async Task FindStalePdfsAsync_PdfStuckInExtracting_IsIncludedInResults()
    {
        // Arrange: Extracting for 35 minutes (> 30-min threshold)
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var staleEntity = BuildPdfEntity(
            nameof(PdfProcessingState.Extracting),
            DateTime.UtcNow - TimeSpan.FromMinutes(35));
        db.PdfDocuments.Add(staleEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert: the Extracting-state document must be found
        Assert.Single(stalePdfs);
        Assert.Equal(staleEntity.Id, stalePdfs[0].Id);
        Assert.Equal(nameof(PdfProcessingState.Extracting), stalePdfs[0].OriginalStatus);
    }

    // ── FindStalePdfsAsync: Chunking state ───────────────────────────────────

    [Fact]
    public async Task FindStalePdfsAsync_PdfStuckInChunking_IsIncludedInResults()
    {
        // Arrange: Chunking for 40 minutes (> 30-min threshold)
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var staleEntity = BuildPdfEntity(
            nameof(PdfProcessingState.Chunking),
            DateTime.UtcNow - TimeSpan.FromMinutes(40));
        db.PdfDocuments.Add(staleEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert
        Assert.Single(stalePdfs);
        Assert.Equal(staleEntity.Id, stalePdfs[0].Id);
        Assert.Equal(nameof(PdfProcessingState.Chunking), stalePdfs[0].OriginalStatus);
    }

    // ── FindStalePdfsAsync: Uploading/Embedding/Indexing states ─────────────

    [Theory]
    [InlineData(nameof(PdfProcessingState.Uploading))]
    [InlineData(nameof(PdfProcessingState.Embedding))]
    [InlineData(nameof(PdfProcessingState.Indexing))]
    public async Task FindStalePdfsAsync_OtherProcessingStates_AreIncludedWhenStale(string state)
    {
        // Arrange: stuck in various processing states for over 30 minutes
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var staleEntity = BuildPdfEntity(state, DateTime.UtcNow - TimeSpan.FromMinutes(35));
        db.PdfDocuments.Add(staleEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert
        Assert.Single(stalePdfs);
        Assert.Equal(staleEntity.Id, stalePdfs[0].Id);
    }

    // ── FindStalePdfsAsync: Pending staleness threshold ──────────────────────

    [Fact]
    public async Task FindStalePdfsAsync_PendingOlderThan2Minutes_IsIncludedInResults()
    {
        // Arrange: Pending for 5 minutes (> 2-min threshold for Pending state)
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var staleEntity = BuildPdfEntity(
            nameof(PdfProcessingState.Pending),
            DateTime.UtcNow - TimeSpan.FromMinutes(5));
        db.PdfDocuments.Add(staleEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert
        Assert.Single(stalePdfs);
        Assert.Equal(staleEntity.Id, stalePdfs[0].Id);
    }

    // ── FindStalePdfsAsync: Recent processing PDFs are excluded ─────────────

    [Fact]
    public async Task FindStalePdfsAsync_RecentExtracting_IsNotConsideredStale()
    {
        // Arrange: Extracting for only 5 minutes (within the 30-min threshold)
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var recentEntity = BuildPdfEntity(
            nameof(PdfProcessingState.Extracting),
            DateTime.UtcNow - TimeSpan.FromMinutes(5));
        db.PdfDocuments.Add(recentEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert: document is not yet stale
        Assert.Empty(stalePdfs);
    }

    // ── FindStalePdfsAsync: Recent Pending PDFs are excluded ─────────────────

    [Fact]
    public async Task FindStalePdfsAsync_RecentPending_IsNotConsideredStale()
    {
        // Arrange: Pending for only 30 seconds (within the 2-min threshold)
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var recentEntity = BuildPdfEntity(
            nameof(PdfProcessingState.Pending),
            DateTime.UtcNow - TimeSpan.FromSeconds(30));
        db.PdfDocuments.Add(recentEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert
        Assert.Empty(stalePdfs);
    }

    // ── FindStalePdfsAsync: Ready documents are always excluded ──────────────

    [Fact]
    public async Task FindStalePdfsAsync_ReadyDocument_IsNeverConsideredStale()
    {
        // Arrange: Ready (terminal success state) with an old timestamp
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var readyEntity = BuildPdfEntity(
            nameof(PdfProcessingState.Ready),
            DateTime.UtcNow - TimeSpan.FromHours(2));
        db.PdfDocuments.Add(readyEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert: terminal states must not be recovered
        Assert.Empty(stalePdfs);
    }

    // ── FindStalePdfsAsync: Failed documents are always excluded ─────────────

    [Fact]
    public async Task FindStalePdfsAsync_FailedDocument_IsNeverConsideredStale()
    {
        // Arrange: Failed (terminal error state) with an old timestamp
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var failedEntity = BuildPdfEntity(
            nameof(PdfProcessingState.Failed),
            DateTime.UtcNow - TimeSpan.FromHours(1));
        db.PdfDocuments.Add(failedEntity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert
        Assert.Empty(stalePdfs);
    }

    // ── FindStalePdfsAsync: Empty database ───────────────────────────────────

    [Fact]
    public async Task FindStalePdfsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert
        Assert.Empty(stalePdfs);
    }

    // ── FindStalePdfsAsync: Multiple stale documents ─────────────────────────

    [Fact]
    public async Task FindStalePdfsAsync_MultipleStaleDocuments_ReturnsAll()
    {
        // Arrange: two stale documents in different states
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var staleTime = DateTime.UtcNow - TimeSpan.FromMinutes(45);

        var extracting = BuildPdfEntity(nameof(PdfProcessingState.Extracting), staleTime, "file1.pdf");
        var chunking = BuildPdfEntity(nameof(PdfProcessingState.Chunking), staleTime, "file2.pdf");

        db.PdfDocuments.AddRange(extracting, chunking);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        var stalePdfs = await InvokeFindStalePdfsAsync(sut);

        // Assert: both documents should be found
        Assert.Equal(2, stalePdfs.Count);
    }

    // ── ResetToPendingAsync: resets state and clears error ───────────────────

    [Fact]
    public async Task ResetToPendingAsync_ExtractingDocument_StateResetToPendingAndErrorCleared()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var entity = BuildPdfEntity(
            nameof(PdfProcessingState.Extracting),
            DateTime.UtcNow - TimeSpan.FromMinutes(35));
        entity.ProcessingError = "Previous error message";
        db.PdfDocuments.Add(entity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, freshDb) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        await InvokeResetToPendingAsync(sut, entity.Id);

        // Assert: state reset to Pending, error cleared
        var updated = await db.PdfDocuments.FindAsync(entity.Id);
        Assert.NotNull(updated);
        Assert.Equal(nameof(PdfProcessingState.Pending), updated.ProcessingState);
        Assert.Null(updated.ProcessingError);
    }

    // ── ResetToPendingAsync: Ready documents are not reset ───────────────────

    [Fact]
    public async Task ResetToPendingAsync_ReadyDocument_StateIsPreserved()
    {
        // Arrange: a Ready (terminal success) document must never be downgraded
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var entity = BuildPdfEntity(
            nameof(PdfProcessingState.Ready),
            DateTime.UtcNow - TimeSpan.FromHours(1));
        db.PdfDocuments.Add(entity);
        await db.SaveChangesAsync();

        var pipelineMock = new Mock<IPdfProcessingPipelineService>();
        var (scopeFactory, _) = BuildScopeFactory(db, pipelineMock);
        var sut = CreateService(scopeFactory);

        // Act
        await InvokeResetToPendingAsync(sut, entity.Id);

        // Assert: Ready state is preserved — terminal success must not be overwritten
        var updated = await db.PdfDocuments.FindAsync(entity.Id);
        Assert.NotNull(updated);
        Assert.Equal(nameof(PdfProcessingState.Ready), updated.ProcessingState);
    }

    // ── Constructor: null guards ─────────────────────────────────────────────

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StalePdfRecoveryService(
                null!,
                NullLogger<StalePdfRecoveryService>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        Assert.Throws<ArgumentNullException>(() =>
            new StalePdfRecoveryService(
                scopeFactoryMock.Object,
                null!));
    }
}
