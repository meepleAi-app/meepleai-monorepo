using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for <see cref="ExtractPdfTextCommandHandler"/> (bug B13).
///
/// The handler performs TEXT-ONLY extraction (no chunk / embed / index). It used to stamp the
/// document as <c>Ready</c> (+ <c>ProcessedAt</c>) after extraction, which falsely advertised the
/// document as fully processed and RAG-available while it was actually unsearchable — and, being no
/// longer <c>Pending</c>, the Quartz claim would never resume it. The domain state machine forbids
/// <c>Extracting → Ready</c> (Ready only follows <c>Indexing</c>); the handler bypassed it via a raw
/// string write.
///
/// The fix leaves the document in <c>Extracting</c>. In the happy path the batch callers immediately
/// send <c>IndexPdfCommand</c>, which advances <c>Extracting → Indexing → Ready</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class ExtractPdfTextCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IBlobStorageService> _blobStorageMock = new();
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();

    private readonly Guid _pdfDocumentId = Guid.NewGuid();

    public ExtractPdfTextCommandHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_TextOnlyExtraction_LeavesDocumentExtracting_NotReady()
    {
        // Arrange
        SeedPdfDocument(nameof(PdfProcessingState.Pending));
        SetupBlobStorage();
        var extraction = SetupSuccessfulExtraction();
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ExtractPdfTextCommand(_pdfDocumentId), CancellationToken.None);

        // Assert: text-only extraction must NOT mark the document Ready.
        result.Success.Should().BeTrue();
        result.ProcessingState.Should().Be(nameof(PdfProcessingState.Extracting),
            "text-only extraction leaves the document mid-pipeline; only IndexPdfCommand reaches Ready");

        var reloaded = await _db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == _pdfDocumentId);
        reloaded.ProcessingState.Should().Be(nameof(PdfProcessingState.Extracting),
            "the handler must not stamp a terminal Ready state after a text-only extract");

        // The extracted payload must still be persisted (this is the handler's actual job).
        reloaded.ExtractedText.Should().Be(_expectedFullText);
        reloaded.PageCount.Should().Be(extraction.totalPages);
        reloaded.CharacterCount.Should().Be(extraction.totalCharacters);
        reloaded.ProcessingError.Should().BeNull("a successful extraction clears any prior error");
        result.CharacterCount.Should().Be(extraction.totalCharacters);
        result.PageCount.Should().Be(extraction.totalPages);
    }

    [Fact]
    public void CreateSuccess_ReturnsExtractingProcessingState_NotReady()
    {
        // The success DTO reports the state the handler leaves the document in — Extracting, not Ready.
        var dto = ExtractPdfTextResultDto.CreateSuccess(characterCount: 1234, pageCount: 5);

        dto.Success.Should().BeTrue();
        dto.ProcessingState.Should().Be(nameof(PdfProcessingState.Extracting));
    }

    private ExtractPdfTextCommandHandler CreateHandler()
        => new(
            _db,
            _blobStorageMock.Object,
            _pdfTextExtractorMock.Object,
            NullLogger<ExtractPdfTextCommandHandler>.Instance,
            TimeProvider.System);

    private void SeedPdfDocument(string processingState)
    {
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            PrivateGameId = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/fake/path/test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = processingState,
            UploadedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    private void SetupBlobStorage()
    {
        _blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF header
    }

    private string _expectedFullText = string.Empty;

    private (int totalPages, int totalCharacters) SetupSuccessfulExtraction()
    {
        var pageChunks = new List<PageTextChunk>
        {
            new(PageNumber: 1, Text: "First page rules text.", CharStartIndex: 0, CharEndIndex: 22),
            new(PageNumber: 2, Text: "Second page setup text.", CharStartIndex: 22, CharEndIndex: 45)
        };
        _expectedFullText = string.Join("\n\n", pageChunks.Where(pc => !pc.IsEmpty).Select(pc => pc.Text));

        const int totalPages = 2;
        var totalCharacters = pageChunks.Sum(pc => pc.Text.Length);

        var result = PagedTextExtractionResult.CreateSuccess(
            pageChunks: pageChunks,
            totalPages: totalPages,
            totalCharacters: totalCharacters,
            ocrTriggered: false,
            structuredElements: null);

        _pdfTextExtractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return (totalPages, totalCharacters);
    }
}
