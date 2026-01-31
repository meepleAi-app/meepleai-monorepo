using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for DownloadPdfQuery.
/// Retrieves PDF file stream from blob storage with authorization checks.
/// </summary>
internal class DownloadPdfQueryHandler : IQueryHandler<DownloadPdfQuery, PdfDownloadResult?>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<DownloadPdfQueryHandler> _logger;

    public DownloadPdfQueryHandler(
        MeepleAiDbContext dbContext,
        IBlobStorageService blobStorageService,
        ILogger<DownloadPdfQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfDownloadResult?> Handle(
        DownloadPdfQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Processing DownloadPdfQuery: PdfId={PdfId}, UserId={UserId}",
            query.PdfId, query.UserId);

        // Step 1: Retrieve PDF metadata from database
        var pdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == query.PdfId)
            .Select(p => new
            {
                p.Id,
                p.GameId,
                p.FileName,
                p.FilePath,
                p.ContentType,
                p.UploadedByUserId
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for download", query.PdfId);
            return null;
        }

        // Step 2: Authorization check
        bool isOwner = pdf.UploadedByUserId == query.UserId;

        if (!query.IsAdmin && !isOwner)
        {
            _logger.LogWarning(
                "User {UserId} denied access to download PDF {PdfId} (owner: {OwnerId})",
                query.UserId, query.PdfId, pdf.UploadedByUserId);
            throw new UnauthorizedAccessException($"Access denied to PDF {query.PdfId}");
        }

        // Step 3: Retrieve file stream from blob storage
        var fileStream = await _blobStorageService
            .RetrieveAsync(pdf.Id.ToString("N"), pdf.GameId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        if (fileStream == null)
        {
            _logger.LogError(
                "PDF file not found in storage for {PdfId} at path {FilePath}",
                query.PdfId, pdf.FilePath);
            return null;
        }

        _logger.LogInformation("User {UserId} downloading PDF {PdfId}", query.UserId, query.PdfId);

        return new PdfDownloadResult(
            FileStream: fileStream,
            FileName: pdf.FileName,
            ContentType: pdf.ContentType ?? "application/pdf",
            PdfId: pdf.Id
        );
    }
}
