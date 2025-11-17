using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.WorkflowErrors;

/// <summary>
/// Handles workflow errors retrieval with filtering and pagination.
/// Business logic: Query filters (workflowId, date range), pagination parameters.
/// Infrastructure delegation: Database query, caching via WorkflowErrorLoggingService.
/// Performance: HybridCache with 5-minute expiration.
/// </summary>
public sealed class GetWorkflowErrorsQueryHandler : IQueryHandler<GetWorkflowErrorsQuery, PagedResult<WorkflowErrorDto>>
{
    private readonly IWorkflowErrorLoggingService _errorLoggingService;
    private readonly ILogger<GetWorkflowErrorsQueryHandler> _logger;

    public GetWorkflowErrorsQueryHandler(
        IWorkflowErrorLoggingService errorLoggingService,
        ILogger<GetWorkflowErrorsQueryHandler> logger)
    {
        _errorLoggingService = errorLoggingService;
        _logger = logger;
    }

    public async Task<PagedResult<WorkflowErrorDto>> Handle(GetWorkflowErrorsQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving workflow errors: WorkflowId={WorkflowId}, FromDate={FromDate}, ToDate={ToDate}, Page={Page}, Limit={Limit}",
            query.WorkflowId, query.FromDate, query.ToDate, query.Page, query.Limit);

        // Create query params
        var queryParams = new WorkflowErrorsQueryParams(
            query.WorkflowId,
            query.FromDate,
            query.ToDate,
            query.Page,
            query.Limit);

        // Delegate to infrastructure service for:
        // - Database query with filters
        // - Pagination
        // - HybridCache (5-min expiration)
        var errors = await _errorLoggingService.GetErrorsAsync(queryParams, cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} workflow errors (Total: {Total}, Page: {Page}/{TotalPages})",
            errors.Items.Count, errors.TotalCount, errors.Page, errors.TotalPages);

        return errors;
    }
}
