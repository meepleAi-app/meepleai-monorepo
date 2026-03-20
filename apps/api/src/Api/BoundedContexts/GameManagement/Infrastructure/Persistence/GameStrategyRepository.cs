using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IGameStrategyRepository.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
internal sealed class GameStrategyRepository : RepositoryBase, IGameStrategyRepository
{

    public GameStrategyRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<(IReadOnlyList<GameStrategy> Items, int TotalCount)> GetBySharedGameIdAsync(
        Guid sharedGameId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.GameStrategies
            .AsNoTracking()
            .Where(s => s.SharedGameId == sharedGameId);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(s => s.Upvotes)
            .ThenBy(s => s.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(MapToDomain).ToList();

        return (items, totalCount);
    }

    private static GameStrategy MapToDomain(GameStrategyEntity entity)
    {
        var tags = DeserializeTags(entity.Tags);

        return GameStrategy.Reconstitute(
            entity.Id,
            entity.SharedGameId,
            entity.Title,
            entity.Content,
            entity.Author,
            entity.Upvotes,
            tags,
            entity.CreatedAt);
    }

    private static IReadOnlyList<string> DeserializeTags(string tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson) || string.Equals(tagsJson, "[]", StringComparison.Ordinal))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
