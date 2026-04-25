using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="MechanicGoldenBggTag"/> entities (ADR-051 Sprint 1 / Task 15).
/// </summary>
/// <remarks>
/// Rows follow append/replace semantics — the entity lacks an <c>xmin</c> concurrency token because
/// every sync pass either inserts a new row or leaves a unique <c>(shared_game_id, name)</c> dedupe
/// constraint to resolve collisions. <see cref="UpsertBatchAsync"/> skips existing <c>(game, name)</c>
/// pairs (by design — the interface contract states existing tags not present in the batch
/// are left untouched) and inserts only the truly new pairs.
/// </remarks>
internal sealed class MechanicGoldenBggTagRepository : RepositoryBase, IMechanicGoldenBggTagRepository
{
    public MechanicGoldenBggTagRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<IReadOnlyList<MechanicGoldenBggTag>> GetByGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.MechanicGoldenBggTags
            .AsNoTracking()
            .Where(t => t.SharedGameId == sharedGameId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task UpsertBatchAsync(
        Guid sharedGameId,
        IReadOnlyList<(string Name, string Category)> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            return;
        }

        // Load existing (game, name) pairs so we only insert the truly new ones.
        var incomingNames = tags
            .Select(t => t.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existingNames = await DbContext.MechanicGoldenBggTags
            .AsNoTracking()
            .Where(t => t.SharedGameId == sharedGameId && incomingNames.Contains(t.Name))
            .Select(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingSet = new HashSet<string>(existingNames, StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;

        var toInsert = new List<MechanicGoldenBggTagEntity>();
        var seenInBatch = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, category) in tags)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            if (existingSet.Contains(name) || !seenInBatch.Add(name))
            {
                continue;
            }

            toInsert.Add(new MechanicGoldenBggTagEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = sharedGameId,
                Name = name,
                Category = category,
                ImportedAt = now,
            });
        }

        if (toInsert.Count > 0)
        {
            await DbContext.MechanicGoldenBggTags
                .AddRangeAsync(toInsert, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicGoldenBggTags
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            DbContext.MechanicGoldenBggTags.Remove(entity);
        }
    }

    // === Mapping ===

    private static MechanicGoldenBggTag MapToDomain(MechanicGoldenBggTagEntity entity)
    {
        return MechanicGoldenBggTag.Reconstitute(
            id: entity.Id,
            sharedGameId: entity.SharedGameId,
            name: entity.Name,
            category: entity.Category,
            importedAt: entity.ImportedAt);
    }
}
