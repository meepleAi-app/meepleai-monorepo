using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for per-user RAG configuration persistence.
/// Issue #5311: RAG Config backend persistence.
/// </summary>
public sealed class RagUserConfigRepository : IRagUserConfigRepository
{
    private readonly MeepleAiDbContext _context;

    public RagUserConfigRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<RagUserConfigEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.RagUserConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    public async Task<RagUserConfigEntity> UpsertAsync(Guid userId, string configJson, CancellationToken ct = default)
    {
        var existing = await _context.RagUserConfigs
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
            _context.RagUserConfigs.Add(existing);
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        await _context.RagUserConfigs
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
