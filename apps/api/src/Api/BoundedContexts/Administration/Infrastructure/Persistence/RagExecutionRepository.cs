using Api.BoundedContexts.Administration.Domain.Aggregates.RagExecution;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for RagExecution aggregate.
/// Issue #4459: RAG Query Replay.
/// </summary>
internal sealed class RagExecutionRepository : IRagExecutionRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public RagExecutionRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<RagExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<RagExecution>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RagExecution> AddAsync(RagExecution execution, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<RagExecution>()
            .AddAsync(execution, cancellationToken)
            .ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return execution;
    }
}
