using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;

internal class ToolboxTemplateRepository : RepositoryBase, IToolboxTemplateRepository
{

    public ToolboxTemplateRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ToolboxTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.Set<ToolboxTemplate>()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<List<ToolboxTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        return await DbContext.Set<ToolboxTemplate>()
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<List<ToolboxTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<ToolboxTemplate>()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ToolboxTemplate toolboxTemplate, CancellationToken ct = default)
    {
        await DbContext.Set<ToolboxTemplate>().AddAsync(toolboxTemplate, ct).ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
