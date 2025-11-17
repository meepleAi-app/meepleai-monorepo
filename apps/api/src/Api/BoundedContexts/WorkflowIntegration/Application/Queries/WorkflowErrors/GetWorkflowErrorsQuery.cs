using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.WorkflowErrors;

/// <summary>
/// Query to retrieve workflow errors with filtering and pagination.
/// Supports filtering by workflow ID and date range, with HybridCache support for performance.
/// </summary>
public sealed record GetWorkflowErrorsQuery : IQuery<PagedResult<WorkflowErrorDto>>
{
    public string? WorkflowId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
}
