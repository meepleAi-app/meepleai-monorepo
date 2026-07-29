using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
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

        // Issue #3363: on-demand cover materialization needs the R2 upload pipeline, registered only
        // when STORAGE_PROVIDER=s3. Fail fast with a clear domain error in local-storage mode.
        if (_uploadPipeline is null)
        {
            throw new CoverMaterializationException(
                "Materializzazione cover PDF non disponibile: richiede storage S3/R2 (STORAGE_PROVIDER=s3).");
        }

        var pdf = await _repository.GetByIdAsync(command.PdfDocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());

        byte[] jpeg;
        try
        {
            jpeg = await _mediator
                .Send(new GetPdfPageImageQuery(command.PdfDocumentId, command.PageNumber), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // SmolDocling down/404: non-blocking failure, no "half-generated"
            // cover state — leave PdfDocument untouched.
            throw new CoverMaterializationException("Rendering pagina PDF non disponibile.", ex);
        }

        var webpBytes = await _webpVariantGenerator
            .GenerateWebpAsync(jpeg, PdfCoverExtractor.ThumbnailWidth, PdfCoverExtractor.ThumbnailHeight, cancellationToken)
            .ConfigureAwait(false);

        // Non-null here: the guard at the top of Handle() throws when _uploadPipeline is null.
        var dbKey = await _uploadPipeline!.UploadAsync(command.DbKey, webpBytes, cancellationToken).ConfigureAwait(false);

        // C1 fix: command.PageNumber is 1-based (render/query contract); PdfDocument.CoverPageIndex
        // is documented and persisted as 0-based (see PdfProcessingPipelineService.cs and
        // BackfillPdfCoversJob.cs, which both store result.SelectedPageIndex, a 0-based value).
        // Only the stored index changes here — the GetPdfPageImageQuery call above stays 1-based.
        pdf.MarkCoverGenerated(dbKey, command.PageNumber - 1);

        await _repository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return dbKey;
    }
}
