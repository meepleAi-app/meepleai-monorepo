using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handles <see cref="GetPhotoBatchStatusQuery"/>:
/// loads the batch with its pages, enforces ownership, and builds a
/// <see cref="PhotoBatchStatusDto"/> with pre-signed thumbnail URLs for each page.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.7.
/// </summary>
internal sealed class GetPhotoBatchStatusQueryHandler
    : IQueryHandler<GetPhotoBatchStatusQuery, PhotoBatchStatusDto>
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IBlobStorageService _blob;

    public GetPhotoBatchStatusQueryHandler(
        IPhotoBatchUploadRepository repo,
        IBlobStorageService blob)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _blob = blob ?? throw new ArgumentNullException(nameof(blob));
    }

    public async Task<PhotoBatchStatusDto> Handle(
        GetPhotoBatchStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 1. Load batch with pages (eager-loaded via FindByIdWithPagesAsync)
        var batch = await _repo.FindByIdWithPagesAsync(query.BatchId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PhotoBatchUpload", query.BatchId.ToString());

        // 2. Ownership enforcement — only the batch owner may access status
        if (batch.UserId != query.UserId)
            throw new ForbiddenException("Cannot access another user's photo batch");

        // 3. Build per-page DTOs with parallel pre-signed URL generation
        var gameIdString = batch.GameId.ToString();
        var pageEntries = batch.Pages.OrderBy(p => p.PageNumber).ToArray();

        // Parallel signed URLs — no CancellationToken on GetPresignedDownloadUrlAsync (interface contract)
        var thumbnailUrlTasks = pageEntries
            .Select(p => _blob.GetPresignedDownloadUrlAsync(p.BlobKey, BlobCategory.PhotoBatch, gameIdString, expirySeconds: 900));
        var thumbnailUrls = await Task.WhenAll(thumbnailUrlTasks).ConfigureAwait(false);

        var pageDtos = pageEntries
            .Select((p, idx) => new PhotoPageStatusDto(
                PageNumber: p.PageNumber,
                ThumbnailUrl: thumbnailUrls[idx] ?? string.Empty,
                Confidence: p.Confidence,
                ConfidenceLevel: p.ConfidenceLevel.ToString(),
                Warnings: p.Warnings))
            .ToArray();

        return new PhotoBatchStatusDto(
            BatchId: batch.Id,
            Status: batch.Status.ToString(),
            TotalPages: batch.TotalPages,
            IndexedPages: batch.IndexedPages,
            LowConfidencePages: batch.Pages.Count(p => p.Confidence < 0.7),
            CreatedAt: batch.CreatedAt,
            CompletedAt: batch.CompletedAt,
            Pages: pageDtos);
    }
}
