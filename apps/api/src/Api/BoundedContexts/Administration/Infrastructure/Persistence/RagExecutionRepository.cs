using Api.BoundedContexts.Administration.Domain.Aggregates.RagExecution;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for RagExecution aggregate.
/// Issue #4459: RAG Query Replay.
/// </summary>
internal sealed class RagExecutionRepository : RepositoryBase, IRagExecutionRepository
{

    public RagExecutionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<RagExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<RagExecution>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RagExecution> AddAsync(RagExecution execution, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<RagExecution>()
            .AddAsync(execution, cancellationToken)
            .ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return execution;
    }
}
