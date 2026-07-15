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
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyCoverR2Key_SkipsAndDoesNotCallBlobStorage()
    {
        var evt = new PdfDeletedDomainEvent(Guid.NewGuid(), coverR2Key: "");

        await Handler().Handle(evt, default);

        _blob.Verify(
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespaceCoverR2Key_SkipsAndDoesNotCallBlobStorage()
    {
        var evt = new PdfDeletedDomainEvent(Guid.NewGuid(), coverR2Key: "   ");

        await Handler().Handle(evt, default);

        _blob.Verify(
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // I1 (Important, #2947 holistic review fix): CoverR2Key is now the deterministic
    // "covers/pdf/{id:D}/cover" convention. The physical object written by
    // PdfProcessingPipelineService/PdfCoverUploadPipeline is ONLY the preview
    // variant ("{key}-preview.webp") — there is no more separate thumb object.
    // The handler must evict via the RAW-KEY delete primitive, not the
    // categorized DeleteAsync (which validates + rejects '/' via PathSecurity).

    [Fact]
    public async Task Handle_HappyPath_DeletesPreviewRawKey()
    {
        var pdfId = Guid.NewGuid();
        var key = $"covers/pdf/{pdfId:D}/cover";
        var expectedRawKey = $"{key}-preview.webp";
        _blob.Setup(b => b.DeleteRawKeyAsync(expectedRawKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        await Handler().Handle(evt, default);

        _blob.Verify(b => b.DeleteRawKeyAsync(expectedRawKey, It.IsAny<CancellationToken>()), Times.Once);
        // The defunct thumb object no longer exists on this branch — no separate delete call.
        _blob.Verify(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BlobNotFound_LogsButDoesNotThrow()
    {
        var pdfId = Guid.NewGuid();
        var key = $"covers/pdf/{pdfId:D}/cover";
        var expectedRawKey = $"{key}-preview.webp";
        _blob.Setup(b => b.DeleteRawKeyAsync(expectedRawKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        var act = async () => await Handler().Handle(evt, default);

        await act.Should().NotThrowAsync();
        _blob.Verify(b => b.DeleteRawKeyAsync(expectedRawKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BlobThrows_SwallowsException()
    {
        var pdfId = Guid.NewGuid();
        var key = $"covers/pdf/{pdfId:D}/cover";
        var expectedRawKey = $"{key}-preview.webp";
        _blob.Setup(b => b.DeleteRawKeyAsync(expectedRawKey, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("S3 unreachable"));
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        var act = async () => await Handler().Handle(evt, default);

        await act.Should().NotThrowAsync();
        _blob.Verify(b => b.DeleteRawKeyAsync(expectedRawKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CancellationRequested_PropagatesOperationCanceledException()
    {
        var pdfId = Guid.NewGuid();
        var key = $"covers/pdf/{pdfId:D}/cover";
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _blob.Setup(b => b.DeleteRawKeyAsync(It.IsAny<string>(), cts.Token))
             .ThrowsAsync(new OperationCanceledException(cts.Token));
        var evt = new PdfDeletedDomainEvent(pdfId, key);

        var act = async () => await Handler().Handle(evt, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
