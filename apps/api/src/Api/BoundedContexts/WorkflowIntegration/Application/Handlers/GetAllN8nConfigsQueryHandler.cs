using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class GetAllN8nConfigsQueryHandler : IQueryHandler<GetAllN8nConfigsQuery, List<N8nConfigurationDto>>
{
    private readonly IN8nConfigurationRepository _repository;

    public GetAllN8nConfigsQueryHandler(IN8nConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<N8nConfigurationDto>> Handle(GetAllN8nConfigsQuery query, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync(cancellationToken);
        return configs.Select(MapToDto).ToList();
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
