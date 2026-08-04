using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Resolves the copyright tier for each chunk citation based on license type,
/// document category, and user ownership. Returns enriched citations with
/// CopyrightTier and IsPublic populated.
/// </summary>
internal interface ICopyrightTierResolver
{
    /// <summary>
    /// Resolves copyright tiers for a batch of citations.
    /// Each citation's CopyrightTier and IsPublic are set based on:
    /// 1. License type (CreativeCommons/PublicDomain → Full)
    /// 2. Document category (non-protected categories → Full)
    /// 3. Ownership (uploader + game owner → Full)
    /// 4. Otherwise → Protected
    /// </summary>
    /// <param name="citations">Raw citations from RAG retrieval.</param>
    /// <param name="userId">Current user ID (Guid.Empty for anonymous).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Citations with resolved CopyrightTier and IsPublic values.</returns>
    Task<IReadOnlyList<ChunkCitation>> ResolveAsync(
        IReadOnlyList<ChunkCitation> citations,
        Guid userId,
        CancellationToken ct);

    /// <summary>
    /// Resolves the copyright tier for a single document (same cascade as <see cref="ResolveAsync"/>).
    /// Used to gate non-citation surfaces that must match citation copyright semantics — e.g. the
    /// image-region viewer overlay, whose region boxes are <c>Full</c>-only (#3435 §5quinquies).
    /// Returns <see cref="CopyrightTier.Protected"/> for an unknown document.
    /// </summary>
    Task<CopyrightTier> ResolveTierAsync(string documentId, Guid userId, CancellationToken ct);
}
