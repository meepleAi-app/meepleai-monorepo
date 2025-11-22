using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries;

/// <summary>
/// Query to get all n8n configurations.
/// </summary>
public record GetAllN8nConfigsQuery() : IQuery<List<N8nConfigurationDto>>;
