using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Resolves expansion game IDs via EntityLink relationships.
/// An expansion link is: Source (expansion) --ExpansionOf--> Target (base game).
/// Issue #5588: Expansion priority in RAG search.
/// </summary>
internal sealed class ExpansionGameResolver : IExpansionGameResolver
{
    private readonly IEntityLinkRepository _entityLinkRepository;
    private readonly ILogger<ExpansionGameResolver> _logger;

    public ExpansionGameResolver(
        IEntityLinkRepository entityLinkRepository,
        ILogger<ExpansionGameResolver> logger)
    {
        _entityLinkRepository = entityLinkRepository ?? throw new ArgumentNullException(nameof(entityLinkRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<Guid>> GetExpansionGameIdsAsync(Guid baseGameId, CancellationToken ct)
    {
        try
        {
            // EntityLink: Source (expansion) --ExpansionOf--> Target (base game).
            // We query links FOR the base game, filtering by ExpansionOf type.
            // GetForEntityAsync returns links where entity is source OR target (for bidirectional).
            // ExpansionOf is NOT bidirectional, so we need links where baseGameId is the target.
            var links = await _entityLinkRepository.GetForEntityAsync(
                MeepleEntityType.Game,
                baseGameId,
                linkType: EntityLinkType.ExpansionOf,
                cancellationToken: ct).ConfigureAwait(false);

            // Filter to only links where baseGameId is the target (i.e., "X is expansion OF baseGame")
            var expansionIds = links
                .Where(l => l.TargetEntityId == baseGameId && l.LinkType == EntityLinkType.ExpansionOf)
                .Select(l => l.SourceEntityId)
                .Distinct()
                .ToList();

            if (expansionIds.Count > 0)
            {
                _logger.LogInformation(
                    "Found {Count} expansion game(s) for base game {BaseGameId}",
                    expansionIds.Count, baseGameId);
            }

            return expansionIds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to resolve expansion games for {BaseGameId}, continuing without boost",
                baseGameId);
            return Array.Empty<Guid>();
        }
    }
}
