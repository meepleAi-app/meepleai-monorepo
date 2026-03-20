using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for per-user RAG configuration persistence.
/// Issue #5311: RAG Config backend persistence.
/// </summary>
public sealed class RagUserConfigRepository : RepositoryBase, IRagUserConfigRepository
{

    public RagUserConfigRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<RagUserConfigEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.RagUserConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    public async Task<RagUserConfigEntity> UpsertAsync(Guid userId, string configJson, CancellationToken ct = default)
    {
        var existing = await DbContext.RagUserConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.ConfigJson = configJson;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new RagUserConfigEntity
            {
                UserId = userId,
                ConfigJson = configJson,
            };
            DbContext.RagUserConfigs.Add(existing);
        }

        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        await DbContext.RagUserConfigs
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
