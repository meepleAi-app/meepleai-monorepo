using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.WorkflowErrors;

/// <summary>
/// Query to retrieve a specific workflow error by ID.
/// Returns null if not found. Uses HybridCache for performance (10-minute expiration).
/// </summary>
public sealed record GetWorkflowErrorByIdQuery : IQuery<WorkflowErrorDto?>
{
    public Guid ErrorId { get; init; }
}
