using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Issue #3281 (Task 5): heading-aware chunking wiring for the chunked-upload background
/// pipeline (<see cref="CompleteChunkedUploadCommandHandler.TriggerPdfProcessingAsync"/>).
///
/// Two things are pinned here:
///  1. When <see cref="IAdvancedChunkingService"/> is available in the background scope, the
///     handler must route through <c>HeadingAwareChunker.BuildAsync</c> so persisted
///     <c>text_chunks</c> carry Heading/Level/ParentChunkId (not the flat chunker's output).
///  2. <c>pdf_documents.StructuredElementsJson</c> must actually persist. The <c>pdfDoc</c> the
///     handler mutates comes from a bare <c>db.PdfDocuments.FindAsync(...)</c> under the
///     production <c>QueryTrackingBehavior.NoTracking</c> default (PERF-06) — so it is detached,
///     and a save without an explicit tracked-write is a silent no-op. This test configures the
///     in-memory context with the SAME NoTracking default as production
///     (<c>InfrastructureServiceExtensions.cs:178</c>) and re-queries via a FRESH DbContext
///     instance (new change tracker, same in-memory store) so a masking pre-tracked identity map
///     cannot hide the bug.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class CompleteChunkedUploadCommandHandlerHeadingAwareTests
{
    /// <summary>
    /// Builds an in-memory <see cref="MeepleAiDbContext"/> configured with
    /// <see cref="QueryTrackingBehavior.NoTracking"/> as the default — mirroring the production
    /// Npgsql registration (PERF-06) — so <c>FindAsync</c> returns detached entities exactly like
    /// production, unlike <see cref="TestDbContextFactory"/> (which leaves the EF Core default,
    /// i.e. tracked, and would mask this class of bug).
    /// </summary>
    private static MeepleAiDbContext CreateNoTrackingInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            TestDbContextFactory.CreateMockEventCollector().Object);
    }

    [Fact]
    public async Task TriggerPdfProcessing_WithStructuredElements_PersistsHeadingHierarchyAndStructuredElementsJson()
    {
        // Arrange ----------------------------------------------------------------
        var dbName = $"complete_chunked_heading_{Guid.NewGuid():N}";
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using (var seedDb = CreateNoTrackingInMemoryDbContext(dbName))
        {
            seedDb.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = gameId,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "test.pdf",
                FilePath = "/test/test.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow,
                ProcessingState = nameof(PdfProcessingState.Extracting)
            });
            await seedDb.SaveChangesAsync();
        }

        // The method opens the file with a real FileStream, so a real temp file must exist.
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "dummy pdf bytes");

        try
        {
            var structuredElements = new List<ExtractedElement>
            {
                new("Setup", 1, "Title"),
                new("Place the board in the middle of the table.", 1, "NarrativeText")
            };

            var extractorMock = new Mock<IPdfTextExtractor>();
            extractorMock
                .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                    new[] { new PageTextChunk(1, "Setup\n\nPlace the board in the middle of the table.", 0, 50) },
                    totalPages: 1,
                    totalCharacters: 50,
                    ocrTriggered: false,
                    structuredElements: structuredElements));

            // Structured-content (tables/diagrams) extraction fails so the method returns
            // early without touching ExtractedTables/Diagrams/AtomicRules — out of scope here.
            var tableExtractorMock = new Mock<IPdfTableExtractor>();
            tableExtractorMock
                .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StructuredContentResult.CreateFailure("no structured content in test"));

            // One parent (section-level, Heading "Setup", Level 0) + one child (Level 2) linked via
            // the parent's auto-generated "N"-format Id — mirrors what AdvancedChunkingService
            // actually produces (HierarchicalChunk.Create always self-assigns its own Id).
            var parentMetadata = new ChunkMetadata { Heading = "Setup", Page = 1, ElementType = "Title" };
            var parent = HierarchicalChunk.CreateParent("Setup section", parentMetadata);
            var childMetadata = new ChunkMetadata { Heading = "Setup", Page = 1, ElementType = "NarrativeText" };
            var child = HierarchicalChunk.CreateChild("Place the board in the middle of the table.", 2, childMetadata, parent.Id);

            var advancedChunkingMock = new Mock<IAdvancedChunkingService>();
            advancedChunkingMock
                .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<HierarchicalChunk> { parent, child });

            // Flat fallback chunker: intentionally unconfigured. If the handler regresses to the
            // pre-#3281 flat path, PrepareForEmbedding returns null (Moq default), which the
            // handler already null-coalesces to zero chunks — driving the "zero usable chunks"
            // failure branch instead of the heading-aware path, so this test would fail loudly.
            var chunkingServiceMock = new Mock<ITextChunkingService>();

            var embeddingMock = new Mock<IEmbeddingService>();
            embeddingMock
                .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<string> texts, CancellationToken _) =>
                    EmbeddingResult.CreateSuccess(texts.Select(t => new float[] { 0.1f, 0.2f, 0.3f }).ToList()));

            var sc = new ServiceCollection();
            // Each scope resolves a FRESH DbContext instance backed by the same in-memory store —
            // never the seeding instance — so pdfDoc arrives via FindAsync exactly like production.
            sc.AddScoped<MeepleAiDbContext>(_ => CreateNoTrackingInMemoryDbContext(dbName));
            sc.AddScoped(_ => chunkingServiceMock.Object);
            sc.AddScoped(_ => advancedChunkingMock.Object);
            sc.AddScoped(_ => embeddingMock.Object);
            sc.AddScoped(_ => Mock.Of<IPdfIndexingPipeline>());
            var provider = sc.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var handler = new CompleteChunkedUploadCommandHandler(
                Mock.Of<IChunkedUploadSessionRepository>(),
                CreateNoTrackingInMemoryDbContext(dbName),
                Mock.Of<IBlobStorageService>(),
                Mock.Of<IBackgroundTaskService>(),
                NullLogger<CompleteChunkedUploadCommandHandler>.Instance,
                scopeFactory,
                extractorMock.Object,
                tableExtractorMock.Object,
                Mock.Of<IMediator>(),
                Mock.Of<IPdfDeduplicationService>(),
                TimeProvider.System);

            // Act --------------------------------------------------------------------
            await handler.TriggerPdfProcessingAsync(pdfId.ToString(), tmp, CancellationToken.None);

            // Assert -----------------------------------------------------------------
            // 🔴 Fresh context (new change tracker) — NOT the seeding context — so a pre-tracked
            // identity map cannot mask the detached-write bug this task fixes.
            await using var assertDb = CreateNoTrackingInMemoryDbContext(dbName);

            var updatedPdf = await assertDb.PdfDocuments.FirstOrDefaultAsync(p => p.Id == pdfId);
            updatedPdf.Should().NotBeNull();
            updatedPdf!.StructuredElementsJson.Should().NotBeNull(
                "the pdfDoc mutated during extraction is detached (NoTracking FindAsync) and must be " +
                "explicitly re-attached (db.PdfDocuments.Update) for the write to persist");
            updatedPdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready),
                "once pdfDoc is attached, every downstream mutation on the SAME db instance " +
                "(including the final Ready transition) should persist too");

            var savedChunks = await assertDb.TextChunks
                .Where(tc => tc.PdfDocumentId == pdfId)
                .OrderBy(tc => tc.ChunkIndex)
                .ToListAsync();
            savedChunks.Should().HaveCount(2);

            var parentEntity = savedChunks.Single(c => c.ParentChunkId == null);
            parentEntity.Heading.Should().Be("Setup");
            parentEntity.Level.Should().Be((short)0);
            parentEntity.Id.Should().Be(Guid.ParseExact(parent.Id, "N"));

            var childEntity = savedChunks.Single(c => c.ParentChunkId != null);
            childEntity.Heading.Should().Be("Setup");
            childEntity.Level.Should().Be((short)2);
            childEntity.ParentChunkId.Should().Be(parentEntity.Id);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
