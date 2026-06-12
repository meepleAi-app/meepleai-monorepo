using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure.Entities;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Concrete implementation of <see cref="IPdfIndexingPipeline"/>.
///
/// Handles two paths:
/// <list type="bullet">
///   <item>New document — calls <see cref="VectorDocument.Create"/> (raises
///   <c>VectorDocumentIndexedEvent</c>) then <see cref="IVectorDocumentRepository.AddAsync"/>.</item>
///   <item>Idempotent re-index — reuses the existing aggregate Id via <see cref="VectorDocument.Create"/>
///   and calls <see cref="IVectorDocumentRepository.UpdateAsync"/>.</item>
/// </list>
/// Issue #2244 / epic #2242 Sub #2.
/// </summary>
internal sealed class PdfIndexingPipeline : IPdfIndexingPipeline
{
    private readonly IVectorDocumentRepository _repository;
    private readonly ILogger<PdfIndexingPipeline> _logger;

    public PdfIndexingPipeline(IVectorDocumentRepository repository, ILogger<PdfIndexingPipeline> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    public async Task<VectorDocument> ExecuteAsync(
        PdfDocumentEntity pdfDoc,
        int indexedChunkCount,
        Guid resolvedGameId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDoc);
        if (indexedChunkCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(indexedChunkCount), "Must be > 0");

        var existing = await _repository
            .GetByGameAndSourceAsync(resolvedGameId, pdfDoc.Id, cancellationToken)
            .ConfigureAwait(false);

        var language = string.IsNullOrWhiteSpace(pdfDoc.Language) ? "en" : pdfDoc.Language;

        if (existing is null)
        {
            var domain = VectorDocument.Create(
                id: Guid.NewGuid(),
                gameId: resolvedGameId,
                pdfDocumentId: pdfDoc.Id,
                language: language,
                totalChunks: indexedChunkCount,
                sharedGameId: pdfDoc.SharedGameId);

            await _repository.AddAsync(domain, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "[PdfIndexingPipeline] Created VectorDocument {VectorDocId} for Pdf {PdfId} ({ChunkCount} chunks)",
                domain.Id, pdfDoc.Id, indexedChunkCount);
            return domain;
        }

        // Idempotent re-index: keep the existing aggregate Id but rebuild with fresh chunk count.
        // NOTE: re-index intentionally resets IndexedAt (now), SearchCount (0), LastSearchedAt (null),
        // and Metadata (null) — every re-index is a fresh slate. If analytics field preservation
        // becomes a requirement, introduce a VectorDocument.ReIndex(int newChunkCount) domain method
        // and switch to Rehydrate(existing.IndexedAt, ...) here instead.
        var refreshed = VectorDocument.Create(
            id: existing.Id,
            gameId: resolvedGameId,
            pdfDocumentId: pdfDoc.Id,
            language: language,
            totalChunks: indexedChunkCount,
            sharedGameId: pdfDoc.SharedGameId);

        await _repository.UpdateAsync(refreshed, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "[PdfIndexingPipeline] Re-indexed VectorDocument {VectorDocId} for Pdf {PdfId} ({ChunkCount} chunks)",
            refreshed.Id, pdfDoc.Id, indexedChunkCount);
        return refreshed;
    }
}
