using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.WorkflowErrors;

/// <summary>
/// Handles retrieval of a specific workflow error by ID.
/// Business logic: ID validation.
/// Infrastructure delegation: Database query, caching via WorkflowErrorLoggingService.
/// Performance: HybridCache with 10-minute expiration.
/// </summary>
public sealed class GetWorkflowErrorByIdQueryHandler : IQueryHandler<GetWorkflowErrorByIdQuery, WorkflowErrorDto?>
{
    private readonly IWorkflowErrorLoggingService _errorLoggingService;
    private readonly ILogger<GetWorkflowErrorByIdQueryHandler> _logger;

    public GetWorkflowErrorByIdQueryHandler(
        IWorkflowErrorLoggingService errorLoggingService,
        ILogger<GetWorkflowErrorByIdQueryHandler> logger)
    {
        _errorLoggingService = errorLoggingService ?? throw new ArgumentNullException(nameof(errorLoggingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkflowErrorDto?> Handle(GetWorkflowErrorByIdQuery query, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (query.ErrorId == Guid.Empty)
        {
            _logger.LogWarning("Invalid error ID: empty GUID");
            return null;
        }

        _logger.LogInformation("Retrieving workflow error {ErrorId}", query.ErrorId);

        // Delegate to infrastructure service for:
        // - Database query
        // - HybridCache (10-min expiration)
        var error = await _errorLoggingService.GetErrorByIdAsync(query.ErrorId, cancellationToken);

        if (error == null)
        {
            _logger.LogWarning("Workflow error {ErrorId} not found", query.ErrorId);
        }
        else
        {
            _logger.LogInformation("Retrieved workflow error {ErrorId}: WorkflowId={WorkflowId}", query.ErrorId, error.WorkflowId);
        }

        return error;
    }
}
