using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class GetActiveN8nConfigQueryHandler : IQueryHandler<GetActiveN8nConfigQuery, N8nConfigurationDto?>
{
    private readonly IN8nConfigurationRepository _configRepository;

    public GetActiveN8nConfigQueryHandler(IN8nConfigurationRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public async Task<N8nConfigurationDto?> Handle(GetActiveN8nConfigQuery query, CancellationToken cancellationToken)
    {
        var config = await _configRepository.GetActiveConfigurationAsync(cancellationToken);

        if (config == null)
            return null;

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
