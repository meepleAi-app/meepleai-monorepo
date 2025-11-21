using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class UpdateN8nConfigCommandHandler : ICommandHandler<UpdateN8nConfigCommand, N8nConfigurationDto>
{
    private readonly IN8nConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateN8nConfigCommandHandler(
        IN8nConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<N8nConfigurationDto> Handle(UpdateN8nConfigCommand command, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(command.ConfigId, cancellationToken);
        if (config == null)
            throw new DomainException($"N8nConfiguration with ID {command.ConfigId} not found");

        // Update configuration fields
        WorkflowUrl? baseUrl = !string.IsNullOrWhiteSpace(command.BaseUrl) ? new WorkflowUrl(command.BaseUrl) : null;
        WorkflowUrl? webhookUrl = command.WebhookUrl != null ? new WorkflowUrl(command.WebhookUrl) : null;

        config.UpdateConfiguration(
            name: command.Name,
            baseUrl: baseUrl,
            apiKeyEncrypted: command.ApiKeyEncrypted,
            webhookUrl: webhookUrl
        );

        // Update active status if specified
        if (command.IsActive.HasValue)
        {
            if (command.IsActive.Value)
                config.Activate();
            else
                config.Deactivate();
        }

        await _repository.UpdateAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
