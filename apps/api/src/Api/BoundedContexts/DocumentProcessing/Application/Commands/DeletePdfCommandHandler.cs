using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

public class DeletePdfCommandHandler : ICommandHandler<DeletePdfCommand, PdfDeleteResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly ILogger<DeletePdfCommandHandler> _logger;

    public DeletePdfCommandHandler(
        MeepleAiDbContext db,
        IServiceScopeFactory scopeFactory,
        IBlobStorageService blobStorageService,
        IAiResponseCacheService cacheService,
        ILogger<DeletePdfCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfDeleteResult> Handle(DeletePdfCommand command, CancellationToken cancellationToken)
    {
        var pdfId = command.PdfId;

        try
        {
            var pdfGuid = Guid.Parse(pdfId);
            var pdfDoc = await _db.PdfDocuments
                .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken);

            if (pdfDoc == null)
            {
                return new PdfDeleteResult(false, "PDF not found", null);
            }

            var gameId = pdfDoc.GameId;

            // Delete associated vector document if exists
            var vectorDoc = await _db.VectorDocuments
                .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken);

            if (vectorDoc != null)
            {
                _db.VectorDocuments.Remove(vectorDoc);
                _logger.LogInformation("Removed vector document for PDF {PdfId}", pdfId);

                // Delete vectors from Qdrant
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var qdrantService = scope.ServiceProvider.GetService<IQdrantService>();

                    if (qdrantService != null)
                    {
                        var deleteResult = await qdrantService.DeleteDocumentAsync(pdfId, cancellationToken).ConfigureAwait(false);
                        if (!deleteResult)
                        {
                            _logger.LogWarning("Failed to delete vectors from Qdrant for PDF {PdfId}", pdfId);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Invalid operation deleting vectors from Qdrant for PDF {PdfId}", pdfId);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error deleting vectors from Qdrant for PDF {PdfId}", pdfId);
                }
#pragma warning restore CA1031
            }

            // Delete PDF document record
            _db.PdfDocuments.Remove(pdfDoc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted PDF document record {PdfId}", pdfId);

            // Delegate physical file deletion to BlobStorageService
            try
            {
                await _blobStorageService.DeleteAsync(pdfId, gameId.ToString(), cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting physical file for PDF {PdfId}", pdfId);
            }
#pragma warning restore CA1031

            await InvalidateCacheSafelyAsync(gameId.ToString(), cancellationToken, "PDF deletion").ConfigureAwait(false);

            return new PdfDeleteResult(true, "PDF deleted successfully", gameId.ToString());
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException($"Failed to delete PDF: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    private async Task InvalidateCacheSafelyAsync(string gameId, CancellationToken ct, string operation)
    {
        try
        {
            await _cacheService.InvalidateGameAsync(gameId, ct).ConfigureAwait(false);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning restore CA1031
    }
}
