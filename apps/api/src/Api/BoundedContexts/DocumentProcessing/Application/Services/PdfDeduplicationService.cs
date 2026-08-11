using System.Security.Cryptography;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Default implementation of <see cref="IPdfDeduplicationService"/>.
/// See the interface doc for the dedup rule (global for catalog, per-user for private).
/// </summary>
internal sealed class PdfDeduplicationService : IPdfDeduplicationService
{
    private readonly IPdfDocumentRepository _repo;

    public PdfDeduplicationService(IPdfDocumentRepository repo)
    {
        _repo = repo;
    }

    public async Task<string> ComputeContentHashAsync(Stream content, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(content, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    public async Task<PdfDedupResult> EvaluateAsync(
        string contentHash,
        Guid? sharedGameId,
        Guid? privateGameId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Catalog (shared): dedup GLOBALLY across all users. Private: dedup PER-USER (isolation).
        var existing = sharedGameId.HasValue
            ? await _repo.FindByContentHashAsync(contentHash, cancellationToken).ConfigureAwait(false)
            : await _repo.FindByContentHashForUserAsync(contentHash, userId, cancellationToken).ConfigureAwait(false);

        if (existing is null || existing.ProcessingState == PdfProcessingState.Failed)
        {
            return new PdfDedupResult(PdfDedupDecision.NewUpload, null, contentHash);
        }

        return new PdfDedupResult(PdfDedupDecision.ReuseExisting, existing.Id, contentHash);
    }
}
