using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for creating a new AI model configuration
/// </summary>
/// <remarks>
/// Issue #2567: POST /api/v1/admin/ai-models endpoint handler
/// </remarks>
internal sealed class CreateAiModelCommandHandler : ICommandHandler<CreateAiModelCommand, AiModelDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAiModelCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AiModelDto> Handle(CreateAiModelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var model = AiModelConfiguration.Create(
            modelId: command.ModelId,
            displayName: command.DisplayName,
            provider: command.Provider,
            priority: command.Priority,
            settings: command.Settings,
            isActive: command.IsActive,
            isPrimary: command.IsPrimary
        );

        await _repository.AddAsync(model, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(model);
    }

    private static AiModelDto MapToDto(AiModelConfiguration model)
    {
        return new AiModelDto
        {
            Id = model.Id,
            ModelId = model.ModelId,
            DisplayName = model.DisplayName,
            Provider = model.Provider,
            Priority = model.Priority,
            IsActive = model.IsActive,
            IsPrimary = model.IsPrimary,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            Settings = model.Settings,
            Usage = model.Usage
        };
    }
}
