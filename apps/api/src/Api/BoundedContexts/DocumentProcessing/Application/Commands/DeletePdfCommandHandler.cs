using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal class DeletePdfCommandHandler : ICommandHandler<DeletePdfCommand, PdfDeleteResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly ILogger<DeletePdfCommandHandler> _logger;

    public DeletePdfCommandHandler(
        MeepleAiDbContext db,
        IBlobStorageService blobStorageService,
        IAiResponseCacheService cacheService,
        ILogger<DeletePdfCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfDeleteResult> Handle(DeletePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pdfId = command.PdfId;

        try
        {
            var pdfGuid = Guid.Parse(pdfId);
            var pdfDoc = await _db.PdfDocuments
                .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken).ConfigureAwait(false);

            if (pdfDoc == null)
            {
                return new PdfDeleteResult(false, "PDF not found", null);
            }

            var gameId = pdfDoc.GameId;

            // Delete associated vector document and vectors from Qdrant
            await DeleteVectorDocumentAsync(pdfGuid, pdfId, cancellationToken).ConfigureAwait(false);

            // Delete PDF document record
            _db.PdfDocuments.Remove(pdfDoc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted PDF document record {PdfId}", pdfId);

            // Delegate physical file deletion to BlobStorageService
            var storageGameId = (pdfDoc.PrivateGameId ?? gameId)?.ToString() ?? string.Empty;
            await DeletePhysicalFileAsync(pdfId, storageGameId, cancellationToken).ConfigureAwait(false);

            // Invalidate cache
            await InvalidateCacheSafelyAsync(storageGameId, "PDF deletion", cancellationToken).ConfigureAwait(false);

            return new PdfDeleteResult(true, "PDF deleted successfully", storageGameId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict deleting PDF {PdfId} - treating as already deleted", pdfId);
            return new PdfDeleteResult(false, "PDF not found", null);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException("Failed to delete PDF metadata: Database error occurred.", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CQRS HANDLER BOUNDARY: Wrap all unexpected exceptions during PDF deletion
        // into domain-specific PdfStorageException for consistent error handling upstream
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Unexpected error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException($"Failed to delete PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes vector document from database and Qdrant.
    /// </summary>
    private async Task DeleteVectorDocumentAsync(Guid pdfGuid, string pdfId, CancellationToken cancellationToken)
    {
        var vectorDoc = await _db.VectorDocuments
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken).ConfigureAwait(false);

        if (vectorDoc == null)
        {
            return;
        }

        _db.VectorDocuments.Remove(vectorDoc);
        _logger.LogInformation("Removed vector document for PDF {PdfId}", pdfId);
    }

    /// <summary>
    /// Deletes physical PDF file from blob storage.
    /// </summary>
    private async Task DeletePhysicalFileAsync(string pdfId, string gameId, CancellationToken cancellationToken)
    {
        try
        {
            await _blobStorageService.DeleteAsync(pdfId, gameId, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Physical file deletion is best-effort cleanup;
        // failures must not block PDF metadata deletion (file may already be gone).
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Error deleting physical file for PDF {PdfId}", pdfId);
        }
    }

    private async Task InvalidateCacheSafelyAsync(string gameId, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await _cacheService.InvalidateGameAsync(gameId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: Cache invalidation is best-effort optimization;
        // failures must not interrupt PDF deletion workflow.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
    }
}

