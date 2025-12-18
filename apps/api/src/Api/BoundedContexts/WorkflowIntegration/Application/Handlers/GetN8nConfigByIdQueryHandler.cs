using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

internal class GetN8NConfigByIdQueryHandler : IQueryHandler<GetN8NConfigByIdQuery, N8NConfigurationDto?>
{
    private readonly IN8NConfigurationRepository _repository;

    public GetN8NConfigByIdQueryHandler(IN8NConfigurationRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<N8NConfigurationDto?> Handle(GetN8NConfigByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var config = await _repository.GetByIdAsync(query.ConfigId, cancellationToken).ConfigureAwait(false);
        return config != null ? MapToDto(config) : null;
    }

    private static N8NConfigurationDto MapToDto(N8NConfiguration config)
    {
        return new N8NConfigurationDto(
            Id: config.Id,
            Name: config.Name,
            BaseUrl: config.BaseUrl.Value,
            WebhookUrl: config.WebhookUrl?.Value,
            IsActive: config.IsActive,
            LastTestedAt: config.LastTestedAt,
            LastTestResult: config.LastTestResult,
            CreatedAt: config.CreatedAt,
            UpdatedAt: config.UpdatedAt
        );
    }
}
