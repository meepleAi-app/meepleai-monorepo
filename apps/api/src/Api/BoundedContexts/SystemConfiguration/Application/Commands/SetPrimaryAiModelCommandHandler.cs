using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// #4169: il modello diventa il predefinito del suo provider; gli altri modelli dello stesso
/// provider smettono di esserlo. Un modello inattivo non può esserlo: il selettore legge solo i
/// modelli attivi, e un predefinito inattivo non avrebbe effetto.
/// </summary>
internal sealed class SetPrimaryAiModelCommandHandler : ICommandHandler<SetPrimaryAiModelCommand, AiModelDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetPrimaryAiModelCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AiModelDto> Handle(SetPrimaryAiModelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var model = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AiModel", command.Id.ToString());

        if (!model.IsActive)
        {
            throw new ConflictException(
                $"Activate {model.ModelId} first: an inactive model cannot be the default of its provider.");
        }

        var all = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var other in all.Where(m => m.IsPrimary && m.Id != model.Id
                     && string.Equals(m.Provider, model.Provider, StringComparison.OrdinalIgnoreCase)))
        {
            other.SetPrimary(false);
            await _repository.UpdateAsync(other, cancellationToken).ConfigureAwait(false);
        }

        if (!model.IsPrimary)
        {
            model.SetPrimary(true);
            await _repository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
        }

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
