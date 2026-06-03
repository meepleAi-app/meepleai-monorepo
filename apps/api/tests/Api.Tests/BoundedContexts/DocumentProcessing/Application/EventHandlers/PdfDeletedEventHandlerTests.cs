using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.EventHandlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfDeletedEventHandlerTests
{
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<ILogger<PdfDeletedEventHandler>> _logger = new();

    private PdfDeletedEventHandler Handler() => new(_blob.Object, _logger.Object);

    [Fact]
    public async Task Handle_NullCoverR2Key_SkipsAndDoesNotCallBlobStorage()
    {
        var evt = new PdfDeletedDomainEvent(Guid.NewGuid(), coverR2Key: null);

        await Handler().Handle(evt, default);

        _blob.Verify(
            b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyCoverR2Key_SkipsAndDoesNotCallBlobStorage()
    {
        var evt = new PdfDeletedDomainEvent(Guid.NewGuid(), coverR2Key: "");

        await Handler().Handle(evt, default);

        _blob.Verify(
            b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespaceCoverR2Key_SkipsAndDoesNotCallBlobStorage()
    {
        var evt = new PdfDeletedDomainEvent(Guid.NewGuid(), coverR2Key: "   ");

        await Handler().Handle(evt, default);

        _blob.Verify(
            b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_HappyPath_DeletesBothThumbAndPreview()
    {
        var pdfId = Guid.NewGuid();
        var key = $"pdf-cover-{pdfId}";
        _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), BlobCategory.GameImage, key, It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        await Handler().Handle(evt, default);

        _blob.Verify(b => b.DeleteAsync("thumb.webp", BlobCategory.GameImage, key, It.IsAny<CancellationToken>()), Times.Once);
        _blob.Verify(b => b.DeleteAsync("preview.webp", BlobCategory.GameImage, key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BlobNotFound_LogsButDoesNotThrow()
    {
        var pdfId = Guid.NewGuid();
        var key = $"pdf-cover-{pdfId}";
        _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), BlobCategory.GameImage, key, It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        var act = async () => await Handler().Handle(evt, default);

        await act.Should().NotThrowAsync();
        // Both attempts still made — handler doesn't short-circuit on first false.
        _blob.Verify(b => b.DeleteAsync("thumb.webp", BlobCategory.GameImage, key, It.IsAny<CancellationToken>()), Times.Once);
        _blob.Verify(b => b.DeleteAsync("preview.webp", BlobCategory.GameImage, key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BlobThrows_SwallowsExceptionAndContinuesWithSecondBlob()
    {
        var pdfId = Guid.NewGuid();
        var key = $"pdf-cover-{pdfId}";
        _blob.Setup(b => b.DeleteAsync("thumb.webp", BlobCategory.GameImage, key, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("S3 unreachable"));
        _blob.Setup(b => b.DeleteAsync("preview.webp", BlobCategory.GameImage, key, It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        var act = async () => await Handler().Handle(evt, default);

        await act.Should().NotThrowAsync();
        _blob.Verify(b => b.DeleteAsync("preview.webp", BlobCategory.GameImage, key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CancellationRequested_PropagatesOperationCanceledException()
    {
        var pdfId = Guid.NewGuid();
        var key = $"pdf-cover-{pdfId}";
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), cts.Token))
             .ThrowsAsync(new OperationCanceledException(cts.Token));
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        var act = async () => await Handler().Handle(evt, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
