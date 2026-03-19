using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for toggling AI model active status
/// </summary>
/// <remarks>
/// Issue #2567: PATCH /api/v1/admin/ai-models/{id}/toggle endpoint handler
/// Toggles between active and inactive state
/// </remarks>
internal sealed class ToggleAiModelActiveCommandHandler : ICommandHandler<ToggleAiModelActiveCommand, AiModelDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleAiModelActiveCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AiModelDto> Handle(ToggleAiModelActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var model = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (model is null)
        {
            throw new NotFoundException("AiModel", command.Id.ToString());
        }

        // Business rule: Primary model cannot be deactivated
        if (model.IsPrimary && model.IsActive)
        {
            throw new ConflictException("Cannot deactivate primary AI model. Set another model as primary first.");
        }

        // Toggle active status
        model.SetActive(!model.IsActive);

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
