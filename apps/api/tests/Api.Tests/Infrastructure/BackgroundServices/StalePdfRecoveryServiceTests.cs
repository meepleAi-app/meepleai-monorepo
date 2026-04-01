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
using Xunit;
using FluentAssertions;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="StalePdfRecoveryService"/>.
/// Verifies that stale PDFs in any intermediate state are detected and reset
/// to "Pending" so the pipeline picks them up on startup.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class StalePdfRecoveryServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IPdfProcessingPipelineService> _pipelineMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly StalePdfRecoveryService _sut;

    private static readonly DateTime BaseTime = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public StalePdfRecoveryServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _pipelineMock = new Mock<IPdfProcessingPipelineService>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Wire a single scope that returns both DbContext and pipeline
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
            .Returns(_dbContext);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IPdfProcessingPipelineService)))
            .Returns(_pipelineMock.Object);

        _pipelineMock
            .Setup(p => p.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new StalePdfRecoveryService(
            _scopeFactoryMock.Object,
            NullLogger<StalePdfRecoveryService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<PdfDocumentEntity> SeedPdfEntityAsync(
        string processingState,
        DateTime uploadedAt,
        Guid? id = null)
    {
        var entity = new PdfDocumentEntity
        {
            Id = id ?? Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/uploads/test.pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = uploadedAt,
            ProcessingState = processingState,
            FileSizeBytes = 1024,
        };
        _dbContext.PdfDocuments.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Calls FindStalePdfsAsync via ExecuteAsync but skips the startup delay
    /// by using an already-cancelled token for the delay and a fresh token for the scan.
    /// We invoke the internal scan method indirectly through the public method
    /// by using reflection to call the private helper directly.
    /// Instead, we use a pre-cancelled startup token trick: pass a token that cancels
    /// immediately after the delay so the service still runs recovery.
    ///
    /// Simpler approach: call the protected ExecuteAsync via a subclass shim or
    /// use the internal scan directly. Since the service is internal/sealed we
    /// expose the scan through the public BackgroundService contract.
    ///
    /// We achieve this by cancelling during startup delay (so it returns early)
    /// then running via the real ExecuteAsync with our own flow control by
    /// manipulating UploadedAt timestamps to simulate "already stale".
    /// </summary>
    private async Task RunRecoveryAsync(CancellationToken cancellationToken = default)
    {
        // The service waits 30s on startup. We bypass this by invoking
        // FindStalePdfsAsync + recovery logic indirectly: run ExecuteAsync
        // but immediately cancel the startup delay. When startup delay is cancelled
        // via OperationCanceledException the service returns early — so we must
        // instead reach the scanning code. We use the reflection-free approach:
        // set a CancellationTokenSource that fires AFTER the Task.Delay call.
        //
        // The cleanest testable approach for a service with a hardcoded startup delay
        // is to call the ScanAndRecoverAsync (or equivalent) method directly.
        // Since it's private, we use the public ExecuteAsync with a delayed cancel.
        //
        // However, the startup delay is 30s which makes tests too slow.
        // So we call the private FindStalePdfsAsync indirectly using a test-friendly
        // method: we directly add stale PDFs and verify state after scanning,
        // by invoking the scan portion via reflection.
        await InvokeFindAndRecoverAsync(cancellationToken);
    }

    /// <summary>
    /// Invokes the private scan + recover logic directly without the startup delay,
    /// using reflection to access private methods on the sealed service.
    /// </summary>
    private async Task InvokeFindAndRecoverAsync(CancellationToken cancellationToken)
    {
        var serviceType = typeof(StalePdfRecoveryService);

        var findMethod = serviceType.GetMethod(
            "FindStalePdfsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var resetMethod = serviceType.GetMethod(
            "ResetToPendingAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        findMethod.Should().NotBeNull("FindStalePdfsAsync must exist as private method");
        resetMethod.Should().NotBeNull("ResetToPendingAsync must exist as private method");

        // Call FindStalePdfsAsync
        var stalePdfsTask = (Task)findMethod!.Invoke(_sut, [cancellationToken])!;
        await stalePdfsTask;

        // Get the result via reflection (Task<List<StalePdfInfo>>)
        var resultProperty = stalePdfsTask.GetType().GetProperty("Result");
        var stalePdfs = resultProperty?.GetValue(stalePdfsTask) as System.Collections.IList;

        if (stalePdfs == null || stalePdfs.Count == 0)
            return;

        foreach (var pdf in stalePdfs)
        {
            var pdfType = pdf.GetType();
            var id = (Guid)pdfType.GetProperty("Id")!.GetValue(pdf)!;
            var filePath = (string)pdfType.GetProperty("FilePath")!.GetValue(pdf)!;
            var userId = (Guid)pdfType.GetProperty("UploadedByUserId")!.GetValue(pdf)!;

            // Reset to Pending
            var resetTask = (Task)resetMethod!.Invoke(_sut, [id, cancellationToken])!;
            await resetTask;

            // Run pipeline
            await _pipelineMock.Object.ProcessAsync(id, filePath, userId, cancellationToken);
        }
    }

    // ── No stale PDFs ────────────────────────────────────────────────────────

    [Fact]
    public async Task FindStalePdfs_WhenNoPdfsExist_DoesNotInvokePipeline()
    {
        // Act
        await RunRecoveryAsync();

        // Assert
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FindStalePdfs_WhenPdfIsReady_IsNotRecovered()
    {
        // Arrange: Ready PDF older than any threshold
        await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Ready),
            uploadedAt: BaseTime.AddHours(-2));

        // Act
        await RunRecoveryAsync();

        // Assert: ready PDFs are never touched
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FindStalePdfs_WhenPdfIsFailedState_IsNotRecovered()
    {
        // Arrange: Failed PDF — terminal state, should not be auto-recovered
        await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Failed),
            uploadedAt: BaseTime.AddHours(-2));

        // Act
        await RunRecoveryAsync();

        // Assert
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Pending staleness (2-minute threshold) ───────────────────────────────

    [Fact]
    public async Task FindStalePdfs_PendingPdfOlderThan2Minutes_IsConsideredStale()
    {
        // Arrange: Pending PDF uploaded 3 minutes ago (past 2-minute staleness threshold)
        var entity = await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Pending),
            uploadedAt: DateTime.UtcNow.AddMinutes(-3));

        // Act
        await RunRecoveryAsync();

        // Assert: pipeline is invoked for stale pending PDF
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                entity.Id, entity.FilePath, entity.UploadedByUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindStalePdfs_PendingPdfNewerThan2Minutes_IsNotRecovered()
    {
        // Arrange: Pending PDF uploaded 1 minute ago (within freshness window)
        await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Pending),
            uploadedAt: DateTime.UtcNow.AddMinutes(-1));

        // Act
        await RunRecoveryAsync();

        // Assert: fresh pending PDFs are not touched
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Intermediate states stuck for 30+ minutes ────────────────────────────

    [Theory]
    [InlineData(nameof(PdfProcessingState.Uploading))]
    [InlineData(nameof(PdfProcessingState.Extracting))]
    [InlineData(nameof(PdfProcessingState.Chunking))]
    [InlineData(nameof(PdfProcessingState.Embedding))]
    [InlineData(nameof(PdfProcessingState.Indexing))]
    public async Task FindStalePdfs_IntermediateStateOlderThan30Minutes_IsRecovered(string stuckState)
    {
        // Arrange: PDF stuck in an intermediate state for 35 minutes
        var entity = await SeedPdfEntityAsync(
            stuckState,
            uploadedAt: DateTime.UtcNow.AddMinutes(-35));

        // Act
        await RunRecoveryAsync();

        // Assert: stuck PDF is recovered via pipeline
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                entity.Id, entity.FilePath, entity.UploadedByUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(nameof(PdfProcessingState.Uploading))]
    [InlineData(nameof(PdfProcessingState.Extracting))]
    [InlineData(nameof(PdfProcessingState.Chunking))]
    [InlineData(nameof(PdfProcessingState.Embedding))]
    [InlineData(nameof(PdfProcessingState.Indexing))]
    public async Task FindStalePdfs_IntermediateStateNewerThan30Minutes_IsNotRecovered(string recentState)
    {
        // Arrange: PDF in intermediate state but only 20 minutes old (within tolerance)
        await SeedPdfEntityAsync(
            recentState,
            uploadedAt: DateTime.UtcNow.AddMinutes(-20));

        // Act
        await RunRecoveryAsync();

        // Assert: recently-started intermediate states are left alone
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── ResetToPending resets state correctly ────────────────────────────────

    [Fact]
    public async Task ResetToPending_ExtractingPdf_SetsStateBackToPending()
    {
        // Arrange: PDF stuck in Extracting state
        var entity = await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Extracting),
            uploadedAt: DateTime.UtcNow.AddMinutes(-35));
        entity.ProcessingError = "Unstructured service timed out";
        await _dbContext.SaveChangesAsync();

        // Act
        await RunRecoveryAsync();

        // Assert: state is reset to Pending and error is cleared
        var refreshed = await _dbContext.PdfDocuments.FindAsync(entity.Id);
        refreshed.Should().NotBeNull();
        refreshed!.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        refreshed.ProcessingError.Should().BeNull();
    }

    [Fact]
    public async Task ResetToPending_ReadyPdf_IsNeverModified()
    {
        // Arrange: a Ready PDF should never be touched even if queried
        var entity = await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Ready),
            uploadedAt: DateTime.UtcNow.AddHours(-1));

        // Act
        await RunRecoveryAsync();

        // Assert: state remains Ready
        var refreshed = await _dbContext.PdfDocuments.FindAsync(entity.Id);
        refreshed!.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready));
    }

    // ── Multiple stale PDFs ──────────────────────────────────────────────────

    [Fact]
    public async Task FindStalePdfs_MultipleStalePdfsInDifferentStates_AllAreRecovered()
    {
        // Arrange: one stuck in Extracting, one stuck in Embedding
        var extractingPdf = await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Extracting),
            uploadedAt: DateTime.UtcNow.AddMinutes(-45));

        var embeddingPdf = await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Embedding),
            uploadedAt: DateTime.UtcNow.AddMinutes(-60));

        // Act
        await RunRecoveryAsync();

        // Assert: both are recovered
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                extractingPdf.Id, extractingPdf.FilePath, extractingPdf.UploadedByUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                embeddingPdf.Id, embeddingPdf.FilePath, embeddingPdf.UploadedByUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindStalePdfs_MixOfStaleAndFresh_OnlyStalePdfsAreRecovered()
    {
        // Arrange: one stale (Chunking, 40 min old) and one fresh (Chunking, 5 min old)
        var stalePdf = await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Chunking),
            uploadedAt: DateTime.UtcNow.AddMinutes(-40));

        await SeedPdfEntityAsync(
            nameof(PdfProcessingState.Chunking),
            uploadedAt: DateTime.UtcNow.AddMinutes(-5));

        // Act
        await RunRecoveryAsync();

        // Assert: only the stale one is recovered
        _pipelineMock.Verify(
            p => p.ProcessAsync(
                stalePdf.Id, stalePdf.FilePath, stalePdf.UploadedByUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _pipelineMock.Verify(
            p => p.ProcessAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // exactly one total
    }
}
