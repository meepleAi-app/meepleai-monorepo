using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for setting PDF visibility in the public library.
/// </summary>
internal class SetPdfVisibilityCommandHandler : ICommandHandler<SetPdfVisibilityCommand, SetPdfVisibilityResult>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetPdfVisibilityCommandHandler> _logger;

    public SetPdfVisibilityCommandHandler(
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<SetPdfVisibilityCommandHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SetPdfVisibilityResult> Handle(SetPdfVisibilityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pdf = await _pdfRepository.GetByIdAsync(command.PdfId, cancellationToken).ConfigureAwait(false);

        if (pdf is null)
        {
            _logger.LogWarning("PDF {PdfId} not found when setting visibility", command.PdfId);
            return new SetPdfVisibilityResult(false, "PDF not found", null);
        }

        if (command.IsPublic)
        {
            pdf.MakePublic();
            _logger.LogInformation("PDF {PdfId} marked as public", command.PdfId);
        }
        else
        {
            pdf.MakePrivate();
            _logger.LogInformation("PDF {PdfId} marked as private", command.PdfId);
        }

        await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(SetPdfVisibilityCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.A);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category A)",
                command.PdfId, nameof(SetPdfVisibilityCommandHandler));
            throw new ConflictException(
                $"Document {command.PdfId} was modified by another concurrent operation; please retry.");
        }

        var statusMessage = command.IsPublic
            ? "PDF is now visible in the public library"
            : "PDF is now private";

        return new SetPdfVisibilityResult(true, statusMessage, command.PdfId);
    }
}
