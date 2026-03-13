using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for overriding the detected language of a PDF document.
/// E5-2: Language Intelligence for Game Night Improvvisata.
/// </summary>
internal sealed class OverridePdfLanguageCommandHandler : ICommandHandler<OverridePdfLanguageCommand, Unit>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OverridePdfLanguageCommandHandler> _logger;

    public OverridePdfLanguageCommandHandler(
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<OverridePdfLanguageCommandHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(OverridePdfLanguageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _pdfRepository.GetByIdAsync(command.PdfId, cancellationToken).ConfigureAwait(false);

        if (pdf is null)
        {
            _logger.LogWarning("PDF {PdfId} not found when overriding language", command.PdfId);
            throw new NotFoundException("PdfDocument", command.PdfId.ToString());
        }

        pdf.OverrideLanguage(command.LanguageCode);

        await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PDF {PdfId} language override set to {LanguageCode}",
            command.PdfId,
            command.LanguageCode ?? "(cleared)");

        return Unit.Value;
    }
}
