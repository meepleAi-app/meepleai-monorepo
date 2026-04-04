using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for accepting the copyright disclaimer on a PDF document.
/// Issue #5446: Records disclaimer acceptance timestamp and user.
/// </summary>
internal sealed class AcceptCopyrightDisclaimerCommandHandler : ICommandHandler<AcceptCopyrightDisclaimerCommand, AcceptCopyrightDisclaimerResult>
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

        var pdf = await _pdfRepository.GetByIdAsync(command.PdfDocumentId, cancellationToken).ConfigureAwait(false);

        if (pdf is null)
            throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());

        if (pdf.UploadedByUserId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to accept disclaimer for PDF {PdfId} owned by {OwnerId}",
                command.UserId, command.PdfDocumentId, pdf.UploadedByUserId);
            throw new ForbiddenException("You do not have permission to accept the disclaimer for this PDF document");
        }

        if (pdf.HasAcceptedDisclaimer)
            throw new ConflictException("Copyright disclaimer has already been accepted for this PDF document");

        pdf.AcceptCopyrightDisclaimer(command.UserId);

        await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Copyright disclaimer accepted for PDF {PdfId} by user {UserId}",
            command.PdfDocumentId, command.UserId);

        return new AcceptCopyrightDisclaimerResult(
            Success: true,
            Message: "Copyright disclaimer accepted",
            PdfDocumentId: command.PdfDocumentId);
    }
}
