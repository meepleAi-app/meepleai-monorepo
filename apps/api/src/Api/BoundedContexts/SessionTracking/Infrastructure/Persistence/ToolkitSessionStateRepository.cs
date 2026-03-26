using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IToolkitSessionStateRepository.
/// Issue #5148 — Epic B5.
/// </summary>
internal sealed class ToolkitSessionStateRepository : RepositoryBase, IToolkitSessionStateRepository
{
    public ToolkitSessionStateRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ToolkitSessionState?> GetBySessionAsync(
        Guid sessionId,
        Guid toolkitId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.ToolkitSessionStates
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.ToolkitId == toolkitId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ToolkitSessionState state, CancellationToken cancellationToken = default)
    {
        await DbContext.ToolkitSessionStates
            .AddAsync(state, cancellationToken)
            .ConfigureAwait(false);
    }
}
