using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for ExtractGameMetadataFromPdfByPdfIdQueryHandler: the wizard variant of
/// <see cref="ExtractGameMetadataFromPdfQueryHandler"/> that finds the PDF through its PdfDocument row.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ExtractGameMetadataFromPdfByPdfIdQueryHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _pdfRepoMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly ExtractGameMetadataFromPdfByPdfIdQueryHandler _handler;

    public ExtractGameMetadataFromPdfByPdfIdQueryHandlerTests()
    {
        _handler = new ExtractGameMetadataFromPdfByPdfIdQueryHandler(
            _pdfRepoMock.Object,
            _blobStorageServiceMock.Object,
            _pdfTextExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfByPdfIdQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_CatalogPdf_ReadsTheBlobUnderTheKeyItsFilePathRecords()
    {
        // #5091: the bucket was rebuilt as PrivateGameId ?? GameId, which misses shared-game-{id}: empty metadata.
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        const string storedFileId = "a1c3e5f7b9d04a2c6e8f0b2d4a6c8e0f";
        var resourceKey = $"shared-game-{gameId}";
        _pdfRepoMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocument(
                pdfId,
                gameId,
                new FileName("rules.pdf"),
                $"pdfs/{resourceKey}/{storedFileId}_rules.pdf",
                new FileSize(1024),
                Guid.NewGuid()));
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(storedFileId, BlobCategory.Pdf, resourceKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("%PDF-1.4\ntest content\n%%EOF"u8.ToArray()));
        _pdfTextExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                extractedText: "WINGSPAN - Players: 1-5",
                pageCount: 10,
                characterCount: 23,
                ocrTriggered: false,
                quality: ExtractionQuality.High));
        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameMetadataDto { Title = "Wingspan", MinPlayers = 1, MaxPlayers = 5 });

        var result = await _handler.Handle(new ExtractGameMetadataFromPdfByPdfIdQuery(pdfId, Guid.NewGuid()), CancellationToken.None);

        result.Title.Should().Be("Wingspan", "the blob is under the key FilePath records");
        _pdfTextExtractorMock.Verify(
            p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
