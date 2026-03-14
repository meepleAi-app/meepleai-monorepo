using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;

internal class ToolboxTemplateRepository : IToolboxTemplateRepository
{
    private readonly MeepleAiDbContext _context;

    public ToolboxTemplateRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ToolboxTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<ToolboxTemplate>()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<List<ToolboxTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        return await _context.Set<ToolboxTemplate>()
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<List<ToolboxTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Set<ToolboxTemplate>()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ToolboxTemplate toolboxTemplate, CancellationToken ct = default)
    {
        await _context.Set<ToolboxTemplate>().AddAsync(toolboxTemplate, ct).ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
