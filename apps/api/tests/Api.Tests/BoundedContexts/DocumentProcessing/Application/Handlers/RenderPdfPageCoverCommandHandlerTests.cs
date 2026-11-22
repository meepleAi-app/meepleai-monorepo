using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// #5100: the rendering a cover proposal uses. It has no PDF repository, so it cannot change the PDF; what it must
/// do is render the requested page and upload it as a preview under the given key.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class RenderPdfPageCoverCommandHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IWebpVariantGenerator> _webp = new();
    private readonly Mock<IPdfCoverUploadPipeline> _pipeline = new();

    [Fact]
    public async Task Handle_RendersThePage_EncodesThePreview_AndUploadsItUnderTheKey()
    {
        var pdfId = Guid.NewGuid();
        var jpeg = new byte[] { 0xFF, 0xD8 };
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        _mediator.Setup(m => m.Send(It.Is<GetPdfPageImageQuery>(q => q.PdfDocumentId == pdfId && q.PageNumber == 3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jpeg);
        _webp.Setup(w => w.GenerateWebpAsync(jpeg, PdfCoverExtractor.PreviewWidth, PdfCoverExtractor.PreviewHeight, It.IsAny<CancellationToken>()))
            .ReturnsAsync(webpBytes);
        _pipeline.Setup(p => p.UploadAsync("covers/g/pdf-cover-1", webpBytes, It.IsAny<CancellationToken>()))
            .ReturnsAsync("covers/g/pdf-cover-1");

        var key = await new RenderPdfPageCoverCommandHandler(_mediator.Object, _webp.Object, _pipeline.Object)
            .Handle(new RenderPdfPageCoverCommand(pdfId, 3, "covers/g/pdf-cover-1"), TestContext.Current.CancellationToken);

        key.Should().Be("covers/g/pdf-cover-1");
        _pipeline.Verify(p => p.UploadAsync("covers/g/pdf-cover-1", webpBytes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutTheS3UploadPipeline_FailsWithTheDomainError()
    {
        var act = () => new RenderPdfPageCoverCommandHandler(_mediator.Object, _webp.Object, uploadPipeline: null)
            .Handle(new RenderPdfPageCoverCommand(Guid.NewGuid(), 1, "covers/g/pdf-cover-1"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CoverMaterializationException>();
        _mediator.Verify(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheRendererIsUnreachable_FailsWithTheDomainError()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SmolDocling down"));

        var act = () => new RenderPdfPageCoverCommandHandler(_mediator.Object, _webp.Object, _pipeline.Object)
            .Handle(new RenderPdfPageCoverCommand(Guid.NewGuid(), 1, "covers/g/pdf-cover-1"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CoverMaterializationException>();
        _pipeline.Verify(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
