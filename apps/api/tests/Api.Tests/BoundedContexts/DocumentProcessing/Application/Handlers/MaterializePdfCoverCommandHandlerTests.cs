using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class MaterializePdfCoverCommandHandlerTests
{
    [Fact]
    public async Task Handle_RendersPageEncodesWebpUploadsAndMarks()
    {
        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocumentBuilder().WithId(pdfId).ThatIsCompleted().Build();
        var repo = new Mock<IPdfDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync(pdf);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new byte[] { 0xFF, 0xD8 }); // JPEG magic
        var webp = new Mock<IWebpVariantGenerator>();
        webp.Setup(w => w.GenerateWebpAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // RIFF
        var pipeline = new Mock<IPdfCoverUploadPipeline>();
        pipeline.Setup(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string k, byte[] _, CancellationToken _) => k);
        var uow = new Mock<IUnitOfWork>();

        var handler = new MaterializePdfCoverCommandHandler(repo.Object, mediator.Object, webp.Object, uow.Object, pipeline.Object);
        // PageNumber is 1-based (user-facing / render contract); CoverPageIndex must be
        // stored 0-based to match PdfDocument.CoverPageIndex's documented convention and
        // the two existing writers (PdfProcessingPipelineService, BackfillPdfCoversJob),
        // both of which persist a 0-based SelectedPageIndex. Issue: C1 off-by-one fix.
        var cmd = new MaterializePdfCoverCommand(pdfId, PageNumber: 3, DbKey: "covers/g/pdf-cover");

        var key = await handler.Handle(cmd, CancellationToken.None);

        key.Should().Be("covers/g/pdf-cover");
        pdf.CoverR2Key.Should().Be("covers/g/pdf-cover");
        pdf.CoverPageIndex.Should().Be(2, "CoverPageIndex is 0-based; PageNumber 3 (1-based) stores as index 2");
        pdf.CoverGenerationStatus.Should().Be(PdfCoverGenerationStatus.Generated);
        repo.Verify(r => r.UpdateAsync(pdf, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EncodesWebpAtPreviewDimensionsNotThumbnail()
    {
        // The materialized cover is uploaded under the same key the resolver serves as
        // the -preview.webp variant (600x900). Encoding at thumbnail dimensions (200x300)
        // would serve a low-res image stretched as a preview.
        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocumentBuilder().WithId(pdfId).ThatIsCompleted().Build();
        var repo = new Mock<IPdfDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync(pdf);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new byte[] { 0xFF, 0xD8 });
        var webp = new Mock<IWebpVariantGenerator>();
        webp.Setup(w => w.GenerateWebpAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x52, 0x49, 0x46, 0x46 });
        var pipeline = new Mock<IPdfCoverUploadPipeline>();
        pipeline.Setup(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string k, byte[] _, CancellationToken _) => k);
        var uow = new Mock<IUnitOfWork>();

        var handler = new MaterializePdfCoverCommandHandler(repo.Object, mediator.Object, webp.Object, uow.Object, pipeline.Object);

        await handler.Handle(new MaterializePdfCoverCommand(pdfId, PageNumber: 1, DbKey: "covers/g/pdf-cover"), CancellationToken.None);

        webp.Verify(w => w.GenerateWebpAsync(
                It.IsAny<byte[]>(),
                PdfCoverExtractor.PreviewWidth,
                PdfCoverExtractor.PreviewHeight,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the cover is served as the 600x900 -preview.webp variant, so it must be encoded at preview dimensions, not thumbnail (200x300)");
    }

    /// <summary>
    /// Guards the render call itself stays 1-based (only the persisted index changes).
    /// C1 fix: GetPdfPageImageQuery must still receive the original 1-based PageNumber.
    /// </summary>
    [Fact]
    public async Task Handle_PassesOneBasedPageNumberToRenderQuery()
    {
        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocumentBuilder().WithId(pdfId).ThatIsCompleted().Build();
        var repo = new Mock<IPdfDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync(pdf);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new byte[] { 0xFF, 0xD8 });
        var webp = new Mock<IWebpVariantGenerator>();
        webp.Setup(w => w.GenerateWebpAsync(It.IsAny<byte[]>(), 200, 300, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x52, 0x49, 0x46, 0x46 });
        var pipeline = new Mock<IPdfCoverUploadPipeline>();
        pipeline.Setup(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string k, byte[] _, CancellationToken _) => k);
        var uow = new Mock<IUnitOfWork>();

        var handler = new MaterializePdfCoverCommandHandler(repo.Object, mediator.Object, webp.Object, uow.Object, pipeline.Object);
        var cmd = new MaterializePdfCoverCommand(pdfId, PageNumber: 5, DbKey: "covers/g/pdf-cover");

        await handler.Handle(cmd, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<GetPdfPageImageQuery>(q => q.PageNumber == 5),
            It.IsAny<CancellationToken>()),
            Times.Once,
            "the render query must still use the original 1-based PageNumber");
        pdf.CoverPageIndex.Should().Be(4, "only the persisted CoverPageIndex is 0-based, not the render call");
    }

    [Fact]
    public async Task Handle_SmolDoclingFails_ThrowsCoverMaterializationExceptionAndDoesNotMark()
    {
        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocumentBuilder().WithId(pdfId).ThatIsCompleted().Build();
        var repo = new Mock<IPdfDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync(pdf);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("503"));
        var handler = new MaterializePdfCoverCommandHandler(repo.Object, mediator.Object,
            Mock.Of<IWebpVariantGenerator>(), Mock.Of<IUnitOfWork>(), Mock.Of<IPdfCoverUploadPipeline>());

        var act = () => handler.Handle(new MaterializePdfCoverCommand(pdfId, 3, "k"), CancellationToken.None);

        await act.Should().ThrowAsync<CoverMaterializationException>();
        pdf.CoverGenerationStatus.Should().Be(PdfCoverGenerationStatus.Pending); // non toccato
    }

    /// <summary>
    /// Issue #3363: in local-storage mode the R2 upload pipeline is unregistered, so the optional
    /// ctor param is null. Handle() must fail fast with a clear domain error (not an opaque DI/NRE),
    /// and must NOT render the page or touch the PdfDocument.
    /// </summary>
    [Fact]
    public async Task Handle_LocalStorageNoUploadPipeline_ThrowsCoverMaterializationException()
    {
        var pdfId = Guid.NewGuid();
        var repo = new Mock<IPdfDocumentRepository>();
        var mediator = new Mock<IMediator>();

        var handler = new MaterializePdfCoverCommandHandler(
            repo.Object, mediator.Object, Mock.Of<IWebpVariantGenerator>(), Mock.Of<IUnitOfWork>(),
            uploadPipeline: null);

        var act = () => handler.Handle(new MaterializePdfCoverCommand(pdfId, 3, "k"), CancellationToken.None);

        await act.Should().ThrowAsync<CoverMaterializationException>();
        // Fails before any rendering / repository read.
        mediator.Verify(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
