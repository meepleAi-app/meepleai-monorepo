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
    private readonly IPdfCoverUploadPipeline _uploadPipeline;
    private readonly IUnitOfWork _unitOfWork;

    public MaterializePdfCoverCommandHandler(
        IPdfDocumentRepository repository,
        IMediator mediator,
        IWebpVariantGenerator webpVariantGenerator,
        IPdfCoverUploadPipeline uploadPipeline,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _webpVariantGenerator = webpVariantGenerator ?? throw new ArgumentNullException(nameof(webpVariantGenerator));
        _uploadPipeline = uploadPipeline ?? throw new ArgumentNullException(nameof(uploadPipeline));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<string> Handle(MaterializePdfCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        var dbKey = await _uploadPipeline.UploadAsync(command.DbKey, webpBytes, cancellationToken).ConfigureAwait(false);

        pdf.MarkCoverGenerated(dbKey, command.PageNumber);

        await _repository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return dbKey;
    }
}
