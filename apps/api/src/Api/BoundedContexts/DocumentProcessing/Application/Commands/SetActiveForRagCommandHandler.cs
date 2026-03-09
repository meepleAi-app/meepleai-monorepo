using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for toggling the RAG active flag on a PDF document.
/// Issue #5446: Controls whether document vectors are included in RAG search.
/// </summary>
internal class SetActiveForRagCommandHandler : ICommandHandler<SetActiveForRagCommand, SetActiveForRagResult>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetActiveForRagCommandHandler> _logger;

    public SetActiveForRagCommandHandler(
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<SetActiveForRagCommandHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SetActiveForRagResult> Handle(SetActiveForRagCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _pdfRepository.GetByIdAsync(command.PdfId, cancellationToken).ConfigureAwait(false);

        if (pdf is null)
        {
            _logger.LogWarning("PDF {PdfId} not found when setting RAG active flag", command.PdfId);
            return new SetActiveForRagResult(false, "PDF not found", null);
        }

        pdf.SetActiveForRag(command.IsActive);

        await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var statusMessage = command.IsActive
            ? "Document is now active for RAG search"
            : "Document is now excluded from RAG search";

        _logger.LogInformation("PDF {PdfId} RAG active flag set to {IsActive}", command.PdfId, command.IsActive);

        return new SetActiveForRagResult(true, statusMessage, command.PdfId);
    }
}
