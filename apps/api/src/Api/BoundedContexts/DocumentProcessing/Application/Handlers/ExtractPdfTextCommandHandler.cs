using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for ExtractPdfTextCommand.
/// Retrieves PDF from blob storage and extracts text for existing documents.
/// Used to reprocess PDFs that are stuck in pending status.
/// </summary>
public class ExtractPdfTextCommandHandler : ICommandHandler<ExtractPdfTextCommand, ExtractPdfTextResultDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ILogger<ExtractPdfTextCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public ExtractPdfTextCommandHandler(
        MeepleAiDbContext db,
        IBlobStorageService blobStorage,
        IPdfTextExtractor pdfTextExtractor,
        ILogger<ExtractPdfTextCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ExtractPdfTextResultDto> Handle(ExtractPdfTextCommand command, CancellationToken cancellationToken)
    {
        var pdfId = command.PdfId;
        _logger.LogInformation("Starting text extraction for PDF {PdfId}", pdfId);

        // 1. Retrieve PDF document from database
        var pdf = await _db.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == pdfId, cancellationToken).ConfigureAwait(false);

        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found", pdfId);
            return ExtractPdfTextResultDto.CreateFailure("PDF not found");
        }

        // 2. Retrieve PDF file from blob storage
        var fileStream = await _blobStorage.RetrieveAsync(
            pdfId.ToString("N"),
            pdf.GameId.ToString(),
            cancellationToken).ConfigureAwait(false);

        if (fileStream == null)
        {
            _logger.LogError("PDF file not found in blob storage for {PdfId}", pdfId);
            return ExtractPdfTextResultDto.CreateFailure("PDF file not found in blob storage");
        }

        try
        {
            // 3. Update status to processing
            pdf.ProcessingStatus = "processing";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // 4. Extract text from PDF
            _logger.LogInformation("Extracting text from PDF {PdfId}", pdfId);
            var extractResult = await _pdfTextExtractor.ExtractPagedTextAsync(
                fileStream,
                enableOcrFallback: true,
                cancellationToken).ConfigureAwait(false);

            if (!extractResult.Success)
            {
                _logger.LogError("Text extraction failed for PDF {PdfId}: {Error}",
                    pdfId, extractResult.ErrorMessage);
                pdf.ProcessingStatus = "failed";
                pdf.ProcessingError = extractResult.ErrorMessage;
                pdf.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return ExtractPdfTextResultDto.CreateFailure(extractResult.ErrorMessage ?? "Text extraction failed");
            }

            // 5. Combine all page chunks into full text
            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            // 6. Update PDF document with extracted text
            pdf.ExtractedText = fullText;
            pdf.PageCount = extractResult.TotalPages;
            pdf.CharacterCount = extractResult.TotalCharacters;
            pdf.ProcessingStatus = "completed";
            pdf.ProcessingError = null;
            pdf.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Text extraction completed for PDF {PdfId}: {PageCount} pages, {CharCount} characters",
                pdfId, extractResult.TotalPages, extractResult.TotalCharacters);

            return ExtractPdfTextResultDto.CreateSuccess(
                extractResult.TotalCharacters,
                extractResult.TotalPages);
        }
        finally
        {
            await fileStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
