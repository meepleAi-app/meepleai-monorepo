using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// SP2 task 8 (#3268): pins the <c>ExtractedText ↔ StructuredElementsJson</c> co-write
/// invariant on the admin/re-extract path. <see cref="ExtractPdfTextCommandHandler"/>
/// rewrites <c>pdf.ExtractedText</c> on every re-extraction; without also rewriting
/// <c>pdf.StructuredElementsJson</c> in the same <c>SaveChanges</c>, a re-extract would
/// leave stale/mismatched structured elements behind (the persistence invariant every
/// SP2 chunk-creation writer — Upload/Complete/Pipeline/IndexPdf — already upholds).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class ExtractPdfTextCommandHandlerTests
{
    private static PdfDocumentEntity SeedPdf(
        MeepleAiDbContext db,
        Guid pdfId,
        string? initialStructuredElementsJson = null)
    {
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
            ProcessingState = nameof(PdfProcessingState.Pending),
            StructuredElementsJson = initialStructuredElementsJson,
        };
        db.PdfDocuments.Add(entity);
        db.SaveChanges();
        return entity;
    }

    private static Mock<IBlobStorageService> CreateBlobStorageMock()
    {
        var mock = new Mock<IBlobStorageService>();
        mock.Setup(b => b.RetrieveAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // "%PDF"
        return mock;
    }

    [Fact]
    public async Task Handle_WithStructuredElements_PersistsStructuredElementsJsonInSameSaveChanges()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var pdfId = Guid.NewGuid();
        // Seed with a stale JSON blob to prove re-extraction REPLACES it, not merely
        // leaves whatever was there before.
        var staleElements = new List<ExtractedElement> { new("Stale Title", 1, "Title") };
        SeedPdf(db, pdfId, StructuredElementsPayload.Serialize(staleElements));

        var newElements = new List<ExtractedElement>
        {
            new("Setup", 1, "Title"),
            new("Place the board in the middle of the table.", 1, "NarrativeText"),
        };

        var extractorMock = new Mock<IPdfTextExtractor>();
        extractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                new[] { new PageTextChunk(1, "Setup\nPlace the board in the middle of the table.", 0, 48) },
                totalPages: 1,
                totalCharacters: 48,
                ocrTriggered: false,
                structuredElements: newElements));

        var handler = new ExtractPdfTextCommandHandler(
            db,
            CreateBlobStorageMock().Object,
            extractorMock.Object,
            NullLogger<ExtractPdfTextCommandHandler>.Instance,
            TimeProvider.System);

        // Act
        var result = await handler.Handle(new ExtractPdfTextCommand(pdfId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await db.PdfDocuments.FindAsync(pdfId);
        updated!.ExtractedText.Should().Contain("Setup");
        updated.StructuredElementsJson.Should().NotBeNullOrEmpty(
            "StructuredElementsJson must be co-written alongside ExtractedText in the same SaveChanges");

        var persisted = StructuredElementsPayload.TryDeserialize(updated.StructuredElementsJson);
        persisted.Should().NotBeNull();
        persisted!.Select(e => e.Text).Should().BeEquivalentTo(newElements.Select(e => e.Text),
            "the co-write must reflect the NEW extraction result, not the stale seeded value");
    }

    [Fact]
    public async Task Handle_WithoutStructuredElements_LeavesStructuredElementsJsonNull()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId, initialStructuredElementsJson: null);

        var extractorMock = new Mock<IPdfTextExtractor>();
        extractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                new[] { new PageTextChunk(1, "Flat text with no structured elements.", 0, 40) },
                totalPages: 1,
                totalCharacters: 40,
                ocrTriggered: false,
                structuredElements: null));

        var handler = new ExtractPdfTextCommandHandler(
            db,
            CreateBlobStorageMock().Object,
            extractorMock.Object,
            NullLogger<ExtractPdfTextCommandHandler>.Instance,
            TimeProvider.System);

        // Act
        var result = await handler.Handle(new ExtractPdfTextCommand(pdfId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await db.PdfDocuments.FindAsync(pdfId);
        updated!.ExtractedText.Should().Contain("Flat text");
        updated.StructuredElementsJson.Should().BeNull(
            "StructuredElementsPayload.Serialize returns null for an absent/empty element list");
    }
}
