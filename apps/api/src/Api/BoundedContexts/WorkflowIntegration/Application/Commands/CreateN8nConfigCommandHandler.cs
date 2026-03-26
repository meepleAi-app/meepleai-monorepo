using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands;

internal class CreateN8NConfigCommandHandler : ICommandHandler<CreateN8NConfigCommand, N8NConfigurationDto>
{
    private readonly IN8NConfigurationRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateN8NConfigCommandHandler(
        IN8NConfigurationRepository configRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(configRepository);
        _configRepository = configRepository;
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _unitOfWork = unitOfWork;
    }

    public async Task<N8NConfigurationDto> Handle(CreateN8NConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var baseUrl = new WorkflowUrl(command.BaseUrl);
        WorkflowUrl? webhookUrl = command.WebhookUrl != null ? new WorkflowUrl(command.WebhookUrl) : null;

        var config = new N8NConfiguration(
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
