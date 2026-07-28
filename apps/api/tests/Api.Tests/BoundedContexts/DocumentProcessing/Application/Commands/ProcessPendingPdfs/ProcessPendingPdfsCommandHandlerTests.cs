using Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;

/// <summary>
/// Unit tests for the B14 (#3269) recovery-hardening of <see cref="ProcessPendingPdfsCommandHandler"/>:
/// the handler now selects only genuinely-recoverable docs (Pending, or in-flight-but-stale) and
/// routes each through the claim-protected <see cref="IPdfProcessingPipelineService.ProcessAsync"/>
/// instead of a blind Extract + Index over every non-terminal doc. The atomic Pending-only claim
/// itself (which makes concurrent sweeps race-safe) lives in RelationalPdfClaimService and is
/// covered by its own tests; here we pin the selector + reset + routing decisions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class ProcessPendingPdfsCommandHandlerTests : IAsyncLifetime
{
    private MeepleAiDbContext _db = default!;
    private Mock<IPdfProcessingPipelineService> _pipeline = default!;
    private ProcessPendingPdfsCommandHandler _handler = default!;

    public ValueTask InitializeAsync()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext($"pendingpdfs_{Guid.NewGuid():N}");
        _pipeline = new Mock<IPdfProcessingPipelineService>();
        _pipeline
            .Setup(p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new ProcessPendingPdfsCommandHandler(
            _db, _pipeline.Object, NullLogger<ProcessPendingPdfsCommandHandler>.Instance);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> SeedPdfAsync(string state, DateTime uploadedAt, string? filePath = null)
    {
        var id = Guid.NewGuid();
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = $"{id:N}.pdf",
            FilePath = filePath ?? $"/tmp/{id:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = state,
            UploadedAt = uploadedAt,
        });
        await _db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Handle_RecentInFlightDoc_IsSkipped_NotClaimed()
    {
        // A doc actively being processed (Embedding, uploaded just now) must NOT be recovered — the
        // old blind Extract + Index would race the live pipeline and corrupt its chunks.
        await SeedPdfAsync(nameof(PdfProcessingState.Embedding), DateTime.UtcNow);

        var result = await _handler.Handle(new ProcessPendingPdfsCommand(), CancellationToken.None);

        result.TotalPending.Should().Be(0);
        result.Triggered.Should().Be(0);
        _pipeline.Verify(
            p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PendingDoc_IsProcessedThroughClaimProtectedPipeline()
    {
        var id = await SeedPdfAsync(nameof(PdfProcessingState.Pending), DateTime.UtcNow);

        var result = await _handler.Handle(new ProcessPendingPdfsCommand(), CancellationToken.None);

        result.Triggered.Should().Be(1);
        _pipeline.Verify(
            p => p.ProcessAsync(id, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StaleInFlightDoc_IsResetToPendingThenProcessed()
    {
        var id = await SeedPdfAsync(nameof(PdfProcessingState.Indexing), DateTime.UtcNow.AddMinutes(-31));

        var result = await _handler.Handle(new ProcessPendingPdfsCommand(), CancellationToken.None);

        result.Triggered.Should().Be(1);
        _pipeline.Verify(
            p => p.ProcessAsync(id, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var reloaded = await _db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == id);
        reloaded.ProcessingState.Should().Be(
            nameof(PdfProcessingState.Pending),
            "a stale in-flight doc must be reset to Pending so the pipeline's atomic claim can pick it up");
    }

    [Fact]
    public async Task Handle_DemoMockPlaceholder_IsExcluded()
    {
        await SeedPdfAsync(
            nameof(PdfProcessingState.Pending),
            DateTime.UtcNow,
            filePath: $"{PdfDocumentEntity.DemoMockFilePathPrefix}badsworm/game/rulebook.pdf");

        var result = await _handler.Handle(new ProcessPendingPdfsCommand(), CancellationToken.None);

        result.TotalPending.Should().Be(0);
        _pipeline.Verify(
            p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
