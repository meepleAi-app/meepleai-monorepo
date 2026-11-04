using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// #4169: aggiorna il prezzo di un modello. La verifica risulta fatta oggi, con fonte <c>admin</c>,
/// così il seed non la sovrascrive con un listino più vecchio. Il listino del gateway la vede entro
/// un minuto.
/// </summary>
internal sealed class UpdateAiModelPricingCommandHandler : ICommandHandler<UpdateAiModelPricingCommand, AiModelDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAiModelPricingCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AiModelDto> Handle(UpdateAiModelPricingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var model = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AiModel", command.Id.ToString());

        model.UpdatePricing(command.InputPricePerMillion, command.OutputPricePerMillion);

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
