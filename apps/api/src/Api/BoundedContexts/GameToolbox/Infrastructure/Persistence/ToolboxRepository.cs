using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;

internal class ToolboxRepository : IToolboxRepository
{
    private readonly MeepleAiDbContext _context;

    public ToolboxRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Toolbox?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<Toolbox>()
            .Include(t => t.Tools)
            .Include(t => t.Phases)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<Toolbox?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        return await _context.Set<Toolbox>()
            .Include(t => t.Tools)
            .Include(t => t.Phases)
            .FirstOrDefaultAsync(t => t.GameId == gameId, ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Toolbox toolbox, CancellationToken ct = default)
    {
        await _context.Set<Toolbox>().AddAsync(toolbox, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(Toolbox toolbox, CancellationToken ct = default)
    {
        _context.Set<Toolbox>().Update(toolbox);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
