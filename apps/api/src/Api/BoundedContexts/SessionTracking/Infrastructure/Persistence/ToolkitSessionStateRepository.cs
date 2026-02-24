using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IToolkitSessionStateRepository.
/// Issue #5148 — Epic B5.
/// </summary>
internal sealed class ToolkitSessionStateRepository : IToolkitSessionStateRepository
{
    private readonly MeepleAiDbContext _context;

    public ToolkitSessionStateRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<ToolkitSessionState?> GetBySessionAsync(
        Guid sessionId,
        Guid toolkitId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ToolkitSessionStates
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.ToolkitId == toolkitId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ToolkitSessionState state, CancellationToken cancellationToken = default)
    {
        await _context.ToolkitSessionStates
            .AddAsync(state, cancellationToken)
            .ConfigureAwait(false);
    }
}
