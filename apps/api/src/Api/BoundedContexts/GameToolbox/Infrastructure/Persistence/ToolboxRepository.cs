using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;

internal class ToolboxRepository : RepositoryBase, IToolboxRepository
{

    public ToolboxRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Toolbox?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.Set<Toolbox>()
            .Include(t => t.Tools)
            .Include(t => t.Phases)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<Toolbox?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        return await DbContext.Set<Toolbox>()
            .Include(t => t.Tools)
            .Include(t => t.Phases)
            .FirstOrDefaultAsync(t => t.GameId == gameId, ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Toolbox toolbox, CancellationToken ct = default)
    {
        await DbContext.Set<Toolbox>().AddAsync(toolbox, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(Toolbox toolbox, CancellationToken ct = default)
    {
        DbContext.Set<Toolbox>().Update(toolbox);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
