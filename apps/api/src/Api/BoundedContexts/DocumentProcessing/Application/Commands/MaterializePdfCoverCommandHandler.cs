using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handles <see cref="MaterializePdfCoverCommand"/>: render page → WebP → R2
/// → mark cover generated. See the type's XML doc for the synchronous,
/// non-blocking materialization contract.
/// </summary>
internal sealed class MaterializePdfCoverCommandHandler : ICommandHandler<MaterializePdfCoverCommand, string>
{
    private readonly IPdfDocumentRepository _repository;
    private readonly IMediator _mediator;
    private readonly IWebpVariantGenerator _webpVariantGenerator;
    private readonly IPdfCoverUploadPipeline? _uploadPipeline;
    private readonly IUnitOfWork _unitOfWork;

    public MaterializePdfCoverCommandHandler(
        IPdfDocumentRepository repository,
        IMediator mediator,
        IWebpVariantGenerator webpVariantGenerator,
        IUnitOfWork unitOfWork,
        // Issue #3363: optional — the R2 upload pipeline is registered only when STORAGE_PROVIDER=s3.
        // In local-storage mode it is null and Handle() fails fast with a clear domain error instead of
        // an opaque DI resolution failure.
        IPdfCoverUploadPipeline? uploadPipeline = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _webpVariantGenerator = webpVariantGenerator ?? throw new ArgumentNullException(nameof(webpVariantGenerator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _uploadPipeline = uploadPipeline;
    }

    public async Task<string> Handle(MaterializePdfCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var uploadPipeline = PdfPageCoverRenderer.RequireUploadPipeline(_uploadPipeline);

        var pdf = await _repository.GetByIdAsync(command.PdfDocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());

        var dbKey = await PdfPageCoverRenderer
            .RenderAndUploadAsync(
                _mediator, _webpVariantGenerator, uploadPipeline, command.PdfDocumentId, command.PageNumber, command.DbKey,
                cancellationToken)
            .ConfigureAwait(false);

        // C1 fix: command.PageNumber is 1-based (render/query contract); PdfDocument.CoverPageIndex
        // is documented and persisted as 0-based (see PdfProcessingPipelineService.cs and
        // BackfillPdfCoversJob.cs, which both store result.SelectedPageIndex, a 0-based value).
        // Only the stored index changes here — the GetPdfPageImageQuery call above stays 1-based.
        pdf.MarkCoverGenerated(dbKey, command.PageNumber - 1);
        MeepleAiMetrics.RecordPdfCoverGeneration(MeepleAiMetrics.CoverGenerationOutcomeGenerated);

        await _repository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return dbKey;
    }
}
