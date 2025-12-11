using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries;

/// <summary>
/// Query to get n8n configuration by ID.
/// </summary>
public record GetN8NConfigByIdQuery(Guid ConfigId) : IQuery<N8NConfigurationDto?>;
