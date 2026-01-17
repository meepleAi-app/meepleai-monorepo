using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handler for updating AI model priority (fallback chain ordering)
/// </summary>
/// <remarks>
/// Issue #2567: PATCH /api/v1/admin/ai-models/{id}/priority endpoint handler
/// Lower priority = higher preference (1 = primary, 2 = first fallback, etc.)
/// </remarks>
internal sealed class UpdateModelPriorityCommandHandler : ICommandHandler<UpdateModelPriorityCommand, AiModelDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateModelPriorityCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AiModelDto> Handle(UpdateModelPriorityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var model = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (model is null)
        {
            throw new NotFoundException("AiModel", command.Id.ToString());
        }

        model.UpdatePriority(command.NewPriority);

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
