using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries;

internal record GetActiveN8NConfigQuery() : IQuery<N8NConfigurationDto?>;
