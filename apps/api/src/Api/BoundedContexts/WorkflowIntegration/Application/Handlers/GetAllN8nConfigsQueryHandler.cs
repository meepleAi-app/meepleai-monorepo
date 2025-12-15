using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

internal class GetAllN8NConfigsQueryHandler : IQueryHandler<GetAllN8NConfigsQuery, List<N8NConfigurationDto>>
{
    private readonly IN8NConfigurationRepository _repository;

    public GetAllN8NConfigsQueryHandler(IN8NConfigurationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<N8NConfigurationDto>> Handle(GetAllN8NConfigsQuery query, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return configs.Select(MapToDto).ToList();
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
