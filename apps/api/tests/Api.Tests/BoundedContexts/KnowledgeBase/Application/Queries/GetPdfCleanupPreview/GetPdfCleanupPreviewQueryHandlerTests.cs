using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPdfCleanupPreview;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetPdfCleanupPreview;

/// <summary>
/// Unit tests for <see cref="GetPdfCleanupPreviewQueryHandler"/>. Issue #1529.
/// Covers: PDF-not-found returns null, zero-counts default policy, per-PDF
/// scoping (no cross-PDF bleed), file size + chunk/raptor aggregates.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1529")]
public sealed class GetPdfCleanupPreviewQueryHandlerTests
{
    private readonly MeepleAiDbContext _db;
    private readonly GetPdfCleanupPreviewQueryHandler _sut;

    public GetPdfCleanupPreviewQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _sut = new GetPdfCleanupPreviewQueryHandler(_db);
    }

    [Fact]
    public async Task Handle_PdfDoesNotExist_ReturnsNull()
    {
        var result = await _sut.Handle(
            new GetPdfCleanupPreviewQuery(Guid.NewGuid()),
            CancellationToken.None);

        result.Should().BeNull("the endpoint maps null → 404");
    }

    [Fact]
    public async Task Handle_PdfExists_NoChunksOrRaptor_ReturnsZeroCounts_NullDefaultPolicy()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        const long fileSize = 47_185_920L;

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "Rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = fileSize,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Pending"
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.Handle(
            new GetPdfCleanupPreviewQuery(pdfId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.PdfId.Should().Be(pdfId);
        result.PdfFileSizeBytes.Should().Be(fileSize);
        result.ChunkCount.Should().Be(0, "no TextChunks seeded — null-default policy emits 0, not null");
        result.RaptorSummaryCount.Should().Be(0, "no RaptorSummaries seeded — null-default policy emits 0");
        result.GraphEdgeCount.Should().Be(0, "constant 0 placeholder — graph store not implemented yet");
    }

    [Fact]
    public async Task Handle_PdfExists_WithChunksAndRaptor_CountsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "Rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = 12_345_678L,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready"
        });

        // 312 chunks (per the issue example)
        for (int i = 0; i < 312; i++)
        {
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                Content = $"chunk-{i}",
                ChunkIndex = i,
                CharacterCount = 100
            });
        }

        // 12 RAPTOR summaries (mix of tree levels)
        for (int i = 0; i < 12; i++)
        {
            _db.RaptorSummaries.Add(new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                GameId = gameId,
                TreeLevel = i % 3,
                ClusterIndex = i,
                SummaryText = $"summary-{i}",
                SourceChunkCount = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.Handle(
            new GetPdfCleanupPreviewQuery(pdfId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.PdfFileSizeBytes.Should().Be(12_345_678L);
        result.ChunkCount.Should().Be(312);
        result.RaptorSummaryCount.Should().Be(12);
        result.GraphEdgeCount.Should().Be(0, "graph store not yet implemented");
    }

    [Fact]
    public async Task Handle_ChunksAndRaptorAreScopedPerPdf_NoBleedAcrossPdfs()
    {
        // Regression guard: COUNT(*) must filter on PdfDocumentId, not return all chunks.
        // Arrange
        var targetPdfId = Guid.NewGuid();
        var unrelatedPdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _db.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = targetPdfId,
                FileName = "Target.pdf",
                FilePath = "/tmp/target.pdf",
                FileSizeBytes = 100,
                UploadedByUserId = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow,
                ProcessingState = "Ready"
            },
            new PdfDocumentEntity
            {
                Id = unrelatedPdfId,
                FileName = "Unrelated.pdf",
                FilePath = "/tmp/unrelated.pdf",
                FileSizeBytes = 200,
                UploadedByUserId = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow,
                ProcessingState = "Ready"
            });

        // 2 chunks on target, 7 on unrelated
        for (int i = 0; i < 2; i++)
        {
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = targetPdfId,
                Content = $"target-{i}",
                ChunkIndex = i,
                CharacterCount = 50
            });
        }
        for (int i = 0; i < 7; i++)
        {
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = unrelatedPdfId,
                Content = $"unrelated-{i}",
                ChunkIndex = i,
                CharacterCount = 50
            });
        }

        // 3 raptor summaries on target, 5 on unrelated
        for (int i = 0; i < 3; i++)
        {
            _db.RaptorSummaries.Add(new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = targetPdfId,
                GameId = gameId,
                TreeLevel = 0,
                ClusterIndex = i,
                SummaryText = $"target-r-{i}",
                SourceChunkCount = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        for (int i = 0; i < 5; i++)
        {
            _db.RaptorSummaries.Add(new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = unrelatedPdfId,
                GameId = gameId,
                TreeLevel = 0,
                ClusterIndex = i,
                SummaryText = $"unrelated-r-{i}",
                SourceChunkCount = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.Handle(
            new GetPdfCleanupPreviewQuery(targetPdfId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.ChunkCount.Should().Be(2, "only chunks belonging to targetPdfId");
        result.RaptorSummaryCount.Should().Be(3, "only raptor summaries belonging to targetPdfId");
        result.PdfFileSizeBytes.Should().Be(100L);
    }
}
