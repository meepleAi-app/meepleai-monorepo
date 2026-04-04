using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for updating an existing AI model configuration
/// </summary>
/// <remarks>
/// Issue #2567: PUT /api/v1/admin/ai-models/{id} endpoint handler
/// </remarks>
internal sealed class UpdateAiModelCommandHandler : ICommandHandler<UpdateAiModelCommand, AiModelDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAiModelCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AiModelDto> Handle(UpdateAiModelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var model = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (model is null)
        {
            throw new NotFoundException("AiModel", command.Id.ToString());
        }

        // Update model properties
        // Note: Entity doesn't expose setters, so we need to use domain methods
        model.UpdatePriority(command.Priority);
        model.SetActive(command.IsActive);
        model.SetPrimary(command.IsPrimary);
        model.UpdateSettings(command.Settings.MaxTokens, command.Settings.Temperature);

        // Update pricing if changed
        if (command.Settings.Pricing != model.Settings.Pricing)
        {
            model.UpdatePricing(
                command.Settings.Pricing.InputPricePerMillion,
                command.Settings.Pricing.OutputPricePerMillion
            );
        }

        await _repository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
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
