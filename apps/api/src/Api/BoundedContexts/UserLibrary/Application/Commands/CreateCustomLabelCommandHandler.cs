using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.Labels;

/// <summary>
/// Handler for creating a custom label for a user.
/// </summary>
internal class CreateCustomLabelCommandHandler : ICommandHandler<CreateCustomLabelCommand, LabelDto>
{
    private readonly IGameLabelRepository _labelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomLabelCommandHandler(
        IGameLabelRepository labelRepository,
        IUnitOfWork unitOfWork)
    {
        _labelRepository = labelRepository ?? throw new ArgumentNullException(nameof(labelRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<LabelDto> Handle(CreateCustomLabelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check if a label with this name already exists for the user
        if (await _labelRepository.LabelNameExistsAsync(command.UserId, command.Name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"A label named '{command.Name}' already exists");
        }

        // Create the label
        var label = GameLabel.CreateCustom(command.UserId, command.Name, command.Color);

        await _labelRepository.AddAsync(label, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new LabelDto(
            Id: label.Id,
            Name: label.Name,
            Color: label.Color,
            IsPredefined: label.IsPredefined,
            CreatedAt: label.CreatedAt
        );
    }
}
