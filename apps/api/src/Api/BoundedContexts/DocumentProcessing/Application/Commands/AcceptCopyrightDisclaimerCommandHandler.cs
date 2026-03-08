using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for accepting the copyright disclaimer on a PDF document.
/// Issue #5446: Records disclaimer acceptance timestamp and user.
/// </summary>
internal class AcceptCopyrightDisclaimerCommandHandler : ICommandHandler<AcceptCopyrightDisclaimerCommand, AcceptCopyrightDisclaimerResult>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcceptCopyrightDisclaimerCommandHandler> _logger;

    public AcceptCopyrightDisclaimerCommandHandler(
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<AcceptCopyrightDisclaimerCommandHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AcceptCopyrightDisclaimerResult> Handle(AcceptCopyrightDisclaimerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _pdfRepository.GetByIdAsync(command.PdfId, cancellationToken).ConfigureAwait(false);

        if (pdf is null)
        {
            _logger.LogWarning("PDF {PdfId} not found when accepting disclaimer", command.PdfId);
            return new AcceptCopyrightDisclaimerResult(false, "PDF not found", null);
        }

        if (pdf.HasAcceptedDisclaimer)
        {
            return new AcceptCopyrightDisclaimerResult(true, "Copyright disclaimer already accepted", command.PdfId);
        }

        pdf.AcceptCopyrightDisclaimer(command.UserId);

        await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Copyright disclaimer accepted for PDF {PdfId} by user {UserId}", command.PdfId, command.UserId);

        return new AcceptCopyrightDisclaimerResult(true, "Copyright disclaimer accepted", command.PdfId);
    }
}
