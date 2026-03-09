using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for reclassifying a document's category, base document, and version label.
/// Issue #5447: Admin can reclassify documents post-upload.
/// </summary>
internal class ReclassifyDocumentCommandHandler : ICommandHandler<ReclassifyDocumentCommand, ReclassifyDocumentResult>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReclassifyDocumentCommandHandler> _logger;

    public ReclassifyDocumentCommandHandler(
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReclassifyDocumentCommandHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReclassifyDocumentResult> Handle(ReclassifyDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<DocumentCategory>(command.Category, ignoreCase: true, out var category))
        {
            return new ReclassifyDocumentResult(false, $"Invalid document category: {command.Category}", null);
        }

        var pdf = await _pdfRepository.GetByIdAsync(command.PdfId, cancellationToken).ConfigureAwait(false);

        if (pdf is null)
        {
            _logger.LogWarning("PDF {PdfId} not found when reclassifying", command.PdfId);
            return new ReclassifyDocumentResult(false, "PDF not found", null);
        }

        pdf.Reclassify(category, command.BaseDocumentId, command.VersionLabel);

        await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PDF {PdfId} reclassified to {Category} with version label {VersionLabel}",
            command.PdfId, category, command.VersionLabel);

        return new ReclassifyDocumentResult(true, "Document reclassified successfully", command.PdfId);
    }
}
