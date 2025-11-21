using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class GetN8nConfigByIdQueryHandler : IQueryHandler<GetN8nConfigByIdQuery, N8nConfigurationDto?>
{
    private readonly IN8nConfigurationRepository _repository;

    public GetN8nConfigByIdQueryHandler(IN8nConfigurationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<N8nConfigurationDto?> Handle(GetN8nConfigByIdQuery query, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(query.ConfigId, cancellationToken);
        return config != null ? MapToDto(config) : null;
    }

    private static N8nConfigurationDto MapToDto(N8nConfiguration config)
    {
        return new N8nConfigurationDto(
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
