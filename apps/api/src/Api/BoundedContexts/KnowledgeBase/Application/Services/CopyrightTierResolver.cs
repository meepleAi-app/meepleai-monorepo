using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Resolves copyright tiers for RAG chunk citations using a cascade of rules:
/// 1. Copyright-free license → Full
/// 2. Non-protected document category → Full
/// 3. Uploader AND game owner → Full
/// 4. Otherwise → Protected
/// </summary>
internal sealed class CopyrightTierResolver : ICopyrightTierResolver
{
    private static readonly HashSet<DocumentCategory> ProtectedCategories = new()
    {
        DocumentCategory.Rulebook,
        DocumentCategory.Expansion,
        DocumentCategory.Errata
    };

    private readonly ICopyrightDataProjection _projection;

    public CopyrightTierResolver(ICopyrightDataProjection projection)
        => _projection = projection;

    public async Task<IReadOnlyList<ChunkCitation>> ResolveAsync(
        IReadOnlyList<ChunkCitation> citations,
        Guid userId,
        CancellationToken ct)
    {
        if (citations.Count == 0) return citations;

        var docIds = citations.Select(c => c.DocumentId).Distinct(StringComparer.Ordinal).ToList();
        var pdfInfos = await _projection.GetPdfCopyrightInfoAsync(docIds, ct).ConfigureAwait(false);

        var gameIds = pdfInfos.Values
            .Select(p => p.GameId ?? p.PrivateGameId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var ownership = userId != Guid.Empty && gameIds.Count > 0
            ? await _projection.CheckOwnershipAsync(userId, gameIds, ct).ConfigureAwait(false)
            : new Dictionary<Guid, bool>();

        return citations.Select(c => ResolveSingle(c, userId, pdfInfos, ownership)).ToList();
    }

    private static ChunkCitation ResolveSingle(
        ChunkCitation citation,
        Guid userId,
        IReadOnlyDictionary<string, PdfCopyrightInfo> pdfInfos,
        IReadOnlyDictionary<Guid, bool> ownership)
    {
        if (!pdfInfos.TryGetValue(citation.DocumentId, out var info))
            return citation with { CopyrightTier = CopyrightTier.Protected, IsPublic = false };

        var tier = ResolveTierCore(info, userId, ownership);
        return citation with { CopyrightTier = tier, IsPublic = info.IsPublic };
    }

    /// <inheritdoc />
    public async Task<CopyrightTier> ResolveTierAsync(string documentId, Guid userId, CancellationToken ct)
    {
        var pdfInfos = await _projection.GetPdfCopyrightInfoAsync(new[] { documentId }, ct).ConfigureAwait(false);
        if (!pdfInfos.TryGetValue(documentId, out var info))
        {
            // Unknown / missing document -> safe fallback (no verbatim, no region highlight).
            return CopyrightTier.Protected;
        }

        var gameId = info.GameId ?? info.PrivateGameId;
        var ownership = userId != Guid.Empty && gameId.HasValue
            ? await _projection.CheckOwnershipAsync(userId, new[] { gameId.Value }, ct).ConfigureAwait(false)
            : new Dictionary<Guid, bool>();

        return ResolveTierCore(info, userId, ownership);
    }

    // Tier cascade — single source of truth shared by ResolveSingle + ResolveTierAsync:
    // 1. copyright-free license -> Full; 2. non-protected category -> Full; 3. uploader AND game
    // owner -> Full; 4. otherwise Protected.
    private static CopyrightTier ResolveTierCore(
        PdfCopyrightInfo info, Guid userId, IReadOnlyDictionary<Guid, bool> ownership)
    {
        // Rule 1: Copyright-free license (CreativeCommons, PublicDomain)
        if (info.LicenseType.IsCopyrightFree())
            return CopyrightTier.Full;

        // Rule 2: Non-protected document category
        if (!ProtectedCategories.Contains(info.DocumentCategory))
            return CopyrightTier.Full;

        // Rule 3: User uploaded AND owns the game (BOTH conditions required)
        if (userId != Guid.Empty)
        {
            var gameId = info.GameId ?? info.PrivateGameId;
            if (gameId.HasValue
                && info.UploadedByUserId == userId
                && ownership.TryGetValue(gameId.Value, out var isOwned)
                && isOwned)
            {
                return CopyrightTier.Full;
            }
        }

        // Default: Protected
        return CopyrightTier.Protected;
    }
}
