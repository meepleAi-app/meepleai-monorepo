using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProcessingPipelineServiceCoverTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfCoverExtractor> _coverExtractor = new();
    private readonly Mock<IPdfCoverUploadPipeline> _coverPipeline = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IDomainEventCollector> _eventCollector = new();
    private readonly List<IDomainEvent> _collected = new();

    public PdfProcessingPipelineServiceCoverTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfPipelineCover_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _eventCollector.Setup(c => c.Collect(It.IsAny<IDomainEvent>()))
                       .Callback<IDomainEvent>(e => _collected.Add(e));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ExtractCoverImageAsync_Generated_UploadsPreviewViaPipelineWithDeterministicKey()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Extracting",
            CoverGenerationStatus = "Pending",
            SharedGameId = Guid.NewGuid(),
        };

        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        _coverExtractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new PdfCoverExtractionResult
                       {
                           Outcome = PdfCoverExtractionOutcome.Generated,
                           ThumbnailWebp = new byte[] { 1 },
                           PreviewWebp = new byte[] { 9, 9, 9 },
                           SelectedPageIndex = 0,
                       });

        var expectedKey = $"covers/pdf/{pdf.Id:D}/cover";
        _coverPipeline.Setup(p => p.UploadAsync(expectedKey, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedKey);

        var sut = PdfProcessingPipelineServiceCoverTestFactory.Create(
            _db, _blob.Object, _coverExtractor.Object, _coverPipeline.Object, _eventCollector.Object);

        await sut.InvokeExtractCoverImageForTestAsync(pdf, "/tmp/rules.pdf", CancellationToken.None);

        _coverPipeline.Verify(p => p.UploadAsync(
            expectedKey,
            It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 9, 9, 9 })),
            It.IsAny<CancellationToken>()), Times.Once);
        _blob.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.GameImage, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        pdf.CoverR2Key.Should().Be(expectedKey);
        pdf.CoverGenerationStatus.Should().Be("Generated");

        _collected.Should().ContainSingle()
            .Which.Should().BeOfType<PdfCoverGeneratedEvent>()
            .Which.CoverR2Key.Should().Be(expectedKey);
    }
}
