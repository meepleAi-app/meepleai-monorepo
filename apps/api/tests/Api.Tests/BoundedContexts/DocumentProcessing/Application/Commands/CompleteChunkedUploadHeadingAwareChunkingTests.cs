using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// SP2 task 6 (#3268): pins that the chunked-upload background pipeline
/// (<see cref="CompleteChunkedUploadCommandHandler.TriggerPdfProcessingAsync"/>)
/// prefers the scope-resolved <see cref="IHeadingAwareChunker"/> over the flat
/// <see cref="ITextChunkingService"/> fallback when it is registered, and that the
/// resulting chunk Heading is persisted on the <c>text_chunks</c> row. Mirrors the
/// wiring already shipped for UploadPdfCommandHandler (task 4) and
/// PdfProcessingPipelineService (task 5).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class CompleteChunkedUploadHeadingAwareChunkingTests
{
    [Fact]
    public async Task TriggerPdfProcessing_WithHeadingAwareChunkerRegistered_PersistsHeadingOnTextChunks()
    {
        // Arrange ----------------------------------------------------------------
        var db = TestDbContextFactory.CreateInMemoryDbContext();

        var pdfId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var entity = new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            UploadedByUserId = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/test/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            ProcessingState = nameof(PdfProcessingState.Extracting)
        };
        db.PdfDocuments.Add(entity);
        await db.SaveChangesAsync();

        // The method opens the file with a real FileStream, so a real temp file
        // must exist on disk.
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
                    new[] { new PageTextChunk(1, "Setup\nPlace the board in the middle of the table.", 0, 48) },
                    totalPages: 1,
                    totalCharacters: 48,
                    ocrTriggered: false,
                    structuredElements: structuredElements));

            var tableExtractorMock = new Mock<IPdfTableExtractor>();
            tableExtractorMock
                .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StructuredContentResult.CreateFailure("no structured content in test"));

            // Flat fallback would produce a heading-less chunk; the heading-aware
            // chunker must be preferred over it when registered.
            var flatChunkingMock = new Mock<ITextChunkingService>();
            flatChunkingMock
                .Setup(c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(new List<DocumentChunkInput>
                {
                    new() { Text = "flat fallback chunk", Page = 1, CharStart = 0, CharEnd = 20, Heading = null }
                });

            var headingChunkerMock = new Mock<IHeadingAwareChunker>();
            headingChunkerMock
                .Setup(c => c.ChunkAsync(pdfId, sharedGameId, structuredElements, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DocumentChunkInput>
                {
                    new() { Text = "Place the board in the middle of the table.", Page = 1, CharStart = 6, CharEnd = 48, Heading = "Setup", Level = 1, ElementType = "NarrativeText" }
                });

            var embeddingMock = new Mock<IEmbeddingService>();
            embeddingMock
                .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f } }));

            var indexingPipelineMock = new Mock<IPdfIndexingPipeline>();
            indexingPipelineMock
                .Setup(p => p.IndexAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sc = new ServiceCollection();
            sc.AddSingleton(db); // MeepleAiDbContext — same instance for the assert
            sc.AddSingleton(flatChunkingMock.Object);
            sc.AddSingleton(headingChunkerMock.Object);
            sc.AddSingleton(embeddingMock.Object);
            sc.AddSingleton(indexingPipelineMock.Object);
            var provider = sc.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var handler = new CompleteChunkedUploadCommandHandler(
                Mock.Of<IChunkedUploadSessionRepository>(),
                db,
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
            var updatedDoc = await db.PdfDocuments.FindAsync(pdfId);
            updatedDoc!.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready));
            updatedDoc.StructuredElementsJson.Should().NotBeNullOrEmpty(
                "StructuredElementsJson must be co-written alongside ExtractedText");

            var persistedChunks = db.TextChunks.Where(tc => tc.PdfDocumentId == pdfId).ToList();
            persistedChunks.Should().HaveCount(1);
            persistedChunks[0].Heading.Should().Be("Setup");

            // The flat fallback must NOT have been used when the heading-aware
            // chunker produced usable chunks.
            flatChunkingMock.Verify(
                c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
                Times.Never);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
