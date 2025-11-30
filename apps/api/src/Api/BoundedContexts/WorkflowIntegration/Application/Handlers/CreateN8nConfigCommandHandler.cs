using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class CreateN8nConfigCommandHandler : ICommandHandler<CreateN8nConfigCommand, N8nConfigurationDto>
{
    private readonly IN8nConfigurationRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateN8nConfigCommandHandler(
        IN8nConfigurationRepository configRepository,
        IUnitOfWork unitOfWork)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<N8nConfigurationDto> Handle(CreateN8nConfigCommand command, CancellationToken cancellationToken)
    {
        var baseUrl = new WorkflowUrl(command.BaseUrl);
        WorkflowUrl? webhookUrl = command.WebhookUrl != null ? new WorkflowUrl(command.WebhookUrl) : null;

        var config = new N8nConfiguration(
            id: Guid.NewGuid(),
            name: command.Name,
            baseUrl: baseUrl,
            apiKeyEncrypted: command.ApiKeyEncrypted,
            createdByUserId: command.CreatedByUserId,
            webhookUrl: webhookUrl
        );

        await _configRepository.AddAsync(config, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(config);
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
