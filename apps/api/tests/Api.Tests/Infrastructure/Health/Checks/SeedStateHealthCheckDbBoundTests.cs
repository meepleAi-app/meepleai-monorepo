using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Health.Checks;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Health.Checks;

/// <summary>
/// DB-bound tests for <see cref="SeedStateHealthCheck.CheckHealthAsync"/>, the
/// path that aggregates the raw counts. The pure state-machine lives in
/// <see cref="SeedStateHealthCheckTests"/>; this class locks down the count
/// SEMANTICS the caller feeds into it — specifically the #2675 regression where
/// <c>embedding_count</c> was the VectorDocuments row-count (per-PDF) instead of
/// the SUM of ChunkCount (per-chunk), pinning any multi-chunk corpus to
/// <c>partial_failed</c> even on a fully-successful bake.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "2675")]
public sealed class SeedStateHealthCheckDbBoundTests
{
    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"SeedStateHealthCheck_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    private static PdfDocumentEntity ReadyPdf(Guid id) => new()
    {
        Id = id,
        FileName = "rules.pdf",
        FilePath = $"test/{id:N}.pdf",
        UploadedByUserId = Guid.NewGuid(),
        UploadedAt = DateTime.UtcNow,
        ProcessingState = "Ready",
        Language = "en",
    };

    private static VectorDocumentEntity IndexedDoc(Guid pdfId, int chunkCount) => new()
    {
        Id = Guid.NewGuid(),
        GameId = Guid.NewGuid(),
        PdfDocumentId = pdfId,
        IndexingStatus = "completed",
        EmbeddingModel = "test-model",
        EmbeddingDimensions = 768,
        ChunkCount = chunkCount,
        IndexedAt = DateTime.UtcNow,
    };

    private static TextChunkEntity Chunk(Guid pdfId, int index) => new()
    {
        Id = Guid.NewGuid(),
        GameId = Guid.NewGuid(),
        PdfDocumentId = pdfId,
        Content = $"chunk {index}",
        ChunkIndex = index,
        CharacterCount = 8,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>A Badsworm dogfood demo mock placeholder: seed/ prefix, no blob, no chunks (#3075).</summary>
    private static PdfDocumentEntity MockPdf(Guid id, string state) => new()
    {
        Id = id,
        FileName = $"rulebook-{id:N}.pdf",
        FilePath = $"{PdfDocumentEntity.DemoMockFilePathPrefix}badsworm/game-{id:N}/rulebook.pdf",
        UploadedByUserId = Guid.NewGuid(),
        UploadedAt = DateTime.UtcNow,
        ProcessingState = state,
        Language = "en",
    };

    private static async Task<HealthCheckResult> RunAsync(MeepleAiDbContext db)
    {
        var check = new SeedStateHealthCheck(db, Mock.Of<ILogger<SeedStateHealthCheck>>());
        return await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
    }

    [Fact]
    public async Task CheckHealthAsync_MultiChunkPdfsAllReady_ReturnsReady()
    {
        // #2675 regression: two Ready PDFs with 3 and 2 chunks respectively.
        // VectorDocuments row-count = 2 (per-PDF), but TextChunks = 5 (per-chunk).
        // Pre-fix embedding_count=2 != chunk_count=5 → false partial_failed.
        // Post-fix embedding_count = sum(ChunkCount) = 3+2 = 5 == 5 → ready.
        await using var db = CreateInMemoryDb(nameof(CheckHealthAsync_MultiChunkPdfsAllReady_ReturnsReady));

        var pdf1 = Guid.NewGuid();
        var pdf2 = Guid.NewGuid();
        db.PdfDocuments.AddRange(ReadyPdf(pdf1), ReadyPdf(pdf2));
        db.VectorDocuments.AddRange(IndexedDoc(pdf1, chunkCount: 3), IndexedDoc(pdf2, chunkCount: 2));
        db.TextChunks.AddRange(
            Chunk(pdf1, 0), Chunk(pdf1, 1), Chunk(pdf1, 2),
            Chunk(pdf2, 0), Chunk(pdf2, 1));
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["seed_state"].Should().Be(SeedStateHealthCheck.SeedStates.Ready);
        result.Data["chunk_count"].Should().Be(5);
        result.Data["embedding_count"].Should().Be(5,
            because: "embedding_count must SUM VectorDocument.ChunkCount, not count rows (#2675)");
    }

    [Fact]
    public async Task CheckHealthAsync_EmbeddingServiceIndexedFewerChunksThanExtracted_ReturnsPartialFailed()
    {
        // The fix must NOT mask a genuine mismatch. Extraction produced 3 chunks
        // but the embedder only recorded 2 → sum(ChunkCount)=2 != chunk_count=3.
        await using var db = CreateInMemoryDb(nameof(CheckHealthAsync_EmbeddingServiceIndexedFewerChunksThanExtracted_ReturnsPartialFailed));

        var pdf = Guid.NewGuid();
        db.PdfDocuments.Add(ReadyPdf(pdf));
        db.VectorDocuments.Add(IndexedDoc(pdf, chunkCount: 2));
        db.TextChunks.AddRange(Chunk(pdf, 0), Chunk(pdf, 1), Chunk(pdf, 2));
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["seed_state"].Should().Be(SeedStateHealthCheck.SeedStates.PartialFailed);
        result.Data["chunk_count"].Should().Be(3);
        result.Data["embedding_count"].Should().Be(2);
    }

    [Fact]
    [Trait("Issue", "3075")]
    public async Task CheckHealthAsync_DemoMockInFailedState_IsExcluded_StaysReady()
    {
        // #3075: Badsworm dogfood mock placeholders (seed/ prefix, 0 chunks) are not real corpus
        // content. A mock flipped to Failed must NOT drag seed_state to partial_failed.
        await using var db = CreateInMemoryDb(nameof(CheckHealthAsync_DemoMockInFailedState_IsExcluded_StaysReady));

        var realPdf = Guid.NewGuid();
        db.PdfDocuments.Add(ReadyPdf(realPdf));
        db.VectorDocuments.Add(IndexedDoc(realPdf, chunkCount: 2));
        db.TextChunks.AddRange(Chunk(realPdf, 0), Chunk(realPdf, 1));
        db.PdfDocuments.Add(MockPdf(Guid.NewGuid(), "Failed")); // demo mock — must be ignored
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["seed_state"].Should().Be(SeedStateHealthCheck.SeedStates.Ready);
        result.Data["pdf_total"].Should().Be(1, because: "demo mocks are excluded from the seed-state counts");
        result.Data["pdf_failed"].Should().Be(0, because: "a Failed demo mock must not degrade seed_state (#3075)");
    }
}
