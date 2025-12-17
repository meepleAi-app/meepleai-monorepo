using Api.Models;

namespace Api.Services;

internal interface IWorkflowErrorLoggingService
{
    /// <summary>
    /// Logs a workflow error from n8n.
    /// </summary>
    Task LogErrorAsync(LogWorkflowErrorRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves workflow errors with filtering and pagination.
    /// </summary>
    Task<PagedResult<WorkflowErrorDto>> GetErrorsAsync(WorkflowErrorsQueryParams queryParams, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific workflow error by ID.
    /// </summary>
    Task<WorkflowErrorDto?> GetErrorByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
