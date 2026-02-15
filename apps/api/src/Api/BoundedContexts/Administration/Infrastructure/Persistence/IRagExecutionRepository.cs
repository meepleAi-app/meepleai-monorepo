using Api.BoundedContexts.Administration.Domain.Aggregates.RagExecution;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// Repository interface for RagExecution aggregate.
/// Issue #4459: RAG Query Replay.
/// </summary>
public interface IRagExecutionRepository
{
    Task<RagExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RagExecution> AddAsync(RagExecution execution, CancellationToken cancellationToken = default);
}
