using System.Security.Cryptography;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Default implementation of <see cref="IPdfDeduplicationService"/>.
/// See the interface doc for the dedup rule (across users among catalog PDFs, same game only for private).
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
        // Catalog (shared): dedup across all users, but only among the PDFs of the catalog (#4326): a private PDF is
        // not a catalog document. Private: only the PDF of the same game (#4317): a private game has its own PDF and
        // its own index. No game named: the user's own PDFs, whatever game.
        var existing = sharedGameId.HasValue
            ? await _repo.FindCatalogByContentHashAsync(contentHash, cancellationToken).ConfigureAwait(false)
            : privateGameId.HasValue
                ? await _repo.FindByContentHashForPrivateGameAsync(contentHash, userId, privateGameId.Value, cancellationToken)
                    .ConfigureAwait(false)
                : await _repo.FindByContentHashForUserAsync(contentHash, userId, cancellationToken).ConfigureAwait(false);

        if (existing is null || existing.ProcessingState == PdfProcessingState.Failed)
        {
            return new PdfDedupResult(PdfDedupDecision.NewUpload, null, contentHash);
        }

        return new PdfDedupResult(PdfDedupDecision.ReuseExisting, existing.Id, contentHash);
    }
}
