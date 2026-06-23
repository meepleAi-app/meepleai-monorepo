using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
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
/// Regression test for the chunked-upload "zero chunk" asymmetry bug.
///
/// The Quartz path (PdfProcessingPipelineService) marks a PDF Failed when text
/// extraction yields zero usable chunks. The chunked-upload path
/// (CompleteChunkedUploadCommandHandler.TriggerPdfProcessingAsync) historically
/// lacked the equivalent guard: with zero chunks the empty-embedding mismatch
/// check (0 == 0) passed, indexing was a no-op, and the PDF reached Ready with no
/// indexed content — an unusable RAG state. This test pins the aligned behavior:
/// zero chunks => Failed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class CompleteChunkedUploadCommandHandlerZeroChunkTests
{
    [Fact]
    public async Task TriggerPdfProcessing_WhenZeroChunksProduced_MarksPdfFailed()
    {
        // Arrange ----------------------------------------------------------------
        var db = TestDbContextFactory.CreateInMemoryDbContext();

        var pdfId = Guid.NewGuid();
        var entity = new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = Guid.NewGuid(),
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
            // Chunking yields ZERO usable chunks — this is the bug scenario.
            var chunkingMock = new Mock<ITextChunkingService>();
            chunkingMock
                .Setup(c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(new List<DocumentChunkInput>());

            // Embedding returns an empty (successful) result. Without the fix the
            // pipeline reaches here, the count mismatch check (0 == 0) passes, and
            // the PDF wrongly advances to Ready.
            var embeddingMock = new Mock<IEmbeddingService>();
            embeddingMock
                .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]>()));

            // Text extraction succeeds with a single non-empty page.
            var extractorMock = new Mock<IPdfTextExtractor>();
            extractorMock
                .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                    new[] { new PageTextChunk(1, "extracted text", 0, 14) },
                    totalPages: 1,
                    totalCharacters: 14,
                    ocrTriggered: false));

            // Structured-content extraction fails (Success=false) so the method
            // returns without serializing tables/diagrams.
            var tableExtractorMock = new Mock<IPdfTableExtractor>();
            tableExtractorMock
                .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StructuredContentResult.CreateFailure("no structured content in test"));

            // Real scope provider supplying the services TriggerPdfProcessingAsync
            // resolves via the scope. IPdfIndexingPipeline is intentionally NOT
            // registered: with zero chunks the internal guard returns before it is
            // resolved.
            var sc = new ServiceCollection();
            sc.AddSingleton(db); // MeepleAiDbContext — same instance for the assert
            sc.AddSingleton(chunkingMock.Object);
            sc.AddSingleton(embeddingMock.Object);
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
                TimeProvider.System);

            // Act --------------------------------------------------------------------
            await handler.TriggerPdfProcessingAsync(pdfId.ToString(), tmp, CancellationToken.None);

            // Assert -----------------------------------------------------------------
            var updated = await db.PdfDocuments.FindAsync(pdfId);
            updated!.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
            updated.ProcessingError.Should().Contain("no usable chunks");
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
