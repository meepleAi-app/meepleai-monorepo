using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #5100: renders <see cref="PageNumber"/> of the given PDF as a cover preview and uploads it under
/// <see cref="DbKey"/>, without changing the PDF. A cover proposal needs the image and nothing else: the PDF's cover
/// stays its own, and no <c>PdfCoverGeneratedEvent</c> carries the page to the game before an admin approves it.
/// <see cref="MaterializePdfCoverCommand"/> renders the same way and then makes the page the PDF's cover.
/// Returns the DB-stored key (no suffix).
/// </summary>
internal sealed record RenderPdfPageCoverCommand(Guid PdfDocumentId, int PageNumber, string DbKey) : ICommand<string>;

internal sealed class RenderPdfPageCoverCommandHandler : ICommandHandler<RenderPdfPageCoverCommand, string>
{
    private readonly IMediator _mediator;
    private readonly IWebpVariantGenerator _webpVariantGenerator;
    private readonly IPdfCoverUploadPipeline? _uploadPipeline;

    public RenderPdfPageCoverCommandHandler(
        IMediator mediator,
        IWebpVariantGenerator webpVariantGenerator,
        // Registered only when STORAGE_PROVIDER=s3, as for MaterializePdfCoverCommandHandler.
        IPdfCoverUploadPipeline? uploadPipeline = null)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _webpVariantGenerator = webpVariantGenerator ?? throw new ArgumentNullException(nameof(webpVariantGenerator));
        _uploadPipeline = uploadPipeline;
    }

    public Task<string> Handle(RenderPdfPageCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var uploadPipeline = PdfPageCoverRenderer.RequireUploadPipeline(_uploadPipeline);

        return PdfPageCoverRenderer.RenderAndUploadAsync(
            _mediator, _webpVariantGenerator, uploadPipeline, command.PdfDocumentId, command.PageNumber, command.DbKey,
            cancellationToken);
    }
}
