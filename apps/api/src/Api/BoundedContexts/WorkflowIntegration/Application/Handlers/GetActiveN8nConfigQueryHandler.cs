using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class GetActiveN8NConfigQueryHandler : IQueryHandler<GetActiveN8NConfigQuery, N8NConfigurationDto?>
{
    private readonly IN8NConfigurationRepository _configRepository;

    public GetActiveN8NConfigQueryHandler(IN8NConfigurationRepository configRepository)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<N8NConfigurationDto?> Handle(GetActiveN8NConfigQuery query, CancellationToken cancellationToken)
    {
        var config = await _configRepository.GetActiveConfigurationAsync(cancellationToken).ConfigureAwait(false);

        if (config == null)
            return null;

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
