using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;

/// <summary>
/// Repository interface for WorkflowErrorLog aggregate.
/// </summary>
public interface IWorkflowErrorLogRepository : IRepository<WorkflowErrorLog, Guid>
{
    /// <summary>
    /// Finds errors by workflow ID.
    /// </summary>
    Task<IReadOnlyList<WorkflowErrorLog>> FindByWorkflowIdAsync(string workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds errors by execution ID.
    /// </summary>
    Task<WorkflowErrorLog?> FindByExecutionIdAsync(string executionId, CancellationToken cancellationToken = default);
}
