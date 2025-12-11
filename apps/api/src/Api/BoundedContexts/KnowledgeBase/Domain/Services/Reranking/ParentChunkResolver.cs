using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;

/// <summary>
/// ADR-016 Phase 4: Resolves parent chunks for expanded context.
/// Uses IChunkRepository to lookup hierarchical relationships.
/// </summary>
public sealed class ParentChunkResolver : IParentChunkResolver
{
    private readonly IChunkRepository _chunkRepository;
    private readonly ILogger<ParentChunkResolver> _logger;

    public ParentChunkResolver(
        IChunkRepository chunkRepository,
        ILogger<ParentChunkResolver> logger)
    {
        _chunkRepository = chunkRepository ?? throw new ArgumentNullException(nameof(chunkRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResolvedParentChunk>> ResolveParentsAsync(
        IEnumerable<string> childChunkIds,
        CancellationToken cancellationToken = default)
    {
        var childIds = childChunkIds?.ToList() ?? throw new ArgumentNullException(nameof(childChunkIds));

        if (childIds.Count == 0)
        {
            return Array.Empty<ResolvedParentChunk>();
        }

        var results = new List<ResolvedParentChunk>();

        // Batch fetch child chunks
        var childChunks = await _chunkRepository.GetByIdsAsync(childIds, cancellationToken).ConfigureAwait(false);

        // Group by parent ID to minimize lookups
        var childrenByParent = childChunks
            .Where(c => c.ParentId != null)
            .GroupBy(c => c.ParentId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (childrenByParent.Count == 0)
        {
            _logger.LogDebug("No parent chunks to resolve for {Count} children (all are root chunks)", childIds.Count);
            return Array.Empty<ResolvedParentChunk>();
        }

        // Batch fetch parent chunks
        var parentChunks = await _chunkRepository.GetByIdsAsync(childrenByParent.Keys, cancellationToken).ConfigureAwait(false);
        var parentsById = parentChunks.ToDictionary(p => p.Id);

        foreach (var (parentId, children) in childrenByParent)
        {
            if (!parentsById.TryGetValue(parentId, out var parent))
            {
                _logger.LogWarning("Parent chunk {ParentId} not found for children", parentId);
                continue;
            }

            foreach (var child in children)
            {
                results.Add(ResolvedParentChunk.FromChunks(parent, child));
            }
        }

        _logger.LogDebug(
            "Resolved {ResolvedCount} parent chunks for {ChildCount} children",
            results.Count,
            childIds.Count);

        return results;
    }

    /// <inheritdoc />
    public async Task<ResolvedParentChunk?> ResolveParentAsync(
        string childChunkId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(childChunkId))
            throw new ArgumentException("Child chunk ID cannot be empty", nameof(childChunkId));

        var child = await _chunkRepository.GetByIdAsync(childChunkId, cancellationToken).ConfigureAwait(false);

        if (child == null)
        {
            _logger.LogDebug("Child chunk {ChunkId} not found", childChunkId);
            return null;
        }

        if (child.ParentId == null)
        {
            _logger.LogDebug("Chunk {ChunkId} is a root chunk with no parent", childChunkId);
            return null;
        }

        var parent = await _chunkRepository.GetParentAsync(childChunkId, cancellationToken).ConfigureAwait(false);

        if (parent == null)
        {
            _logger.LogWarning("Parent chunk not found for child {ChildId}", childChunkId);
            return null;
        }

        return ResolvedParentChunk.FromChunks(parent, child);
    }
}
