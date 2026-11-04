using System.Security.Cryptography;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Default implementation of <see cref="IPdfDeduplicationService"/>.
/// See the interface doc for the dedup rule (across users on the same catalog game, same game only for private).
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
        // Private: only the PDF of the same game (#4317): a private game has its own PDF and its own index. It comes
        // first because that is how a record naming both ids is stored (the chunked upload keeps the private id and
        // drops the catalog one). Catalog: across all users, but only the PDFs of THAT game (#4326): a private PDF is
        // not a catalog document, and retrieval reads a game's documents by its own id. No game named: the user's own
        // PDFs, whatever game.
        var existing = privateGameId.HasValue
            ? await _repo.FindByContentHashForPrivateGameAsync(contentHash, userId, privateGameId.Value, cancellationToken)
                .ConfigureAwait(false)
            : sharedGameId.HasValue
                ? await _repo.FindByContentHashForCatalogGameAsync(contentHash, sharedGameId.Value, cancellationToken)
                    .ConfigureAwait(false)
                : await _repo.FindByContentHashForUserAsync(contentHash, userId, cancellationToken).ConfigureAwait(false);

        if (existing is null || existing.ProcessingState == PdfProcessingState.Failed)
        {
            return new PdfDedupResult(PdfDedupDecision.NewUpload, null, contentHash);
        }

        return new PdfDedupResult(PdfDedupDecision.ReuseExisting, existing.Id, contentHash);
    }
}
