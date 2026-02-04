using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers.Labels;

/// <summary>
/// Handler for deleting a custom label.
/// </summary>
internal class DeleteCustomLabelCommandHandler : ICommandHandler<DeleteCustomLabelCommand, bool>
{
    private readonly IGameLabelRepository _labelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCustomLabelCommandHandler(
        IGameLabelRepository labelRepository,
        IUnitOfWork unitOfWork)
    {
        _labelRepository = labelRepository ?? throw new ArgumentNullException(nameof(labelRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<bool> Handle(DeleteCustomLabelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get the label
        var label = await _labelRepository.GetByIdAsync(command.LabelId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Label with ID {command.LabelId} not found");

        // Verify it's not a predefined label
        if (label.IsPredefined)
        {
            throw new ConflictException("Cannot delete predefined labels");
        }

        // Verify the user owns the label
        if (label.UserId != command.UserId)
        {
            throw new NotFoundException($"Label with ID {command.LabelId} not found");
        }

        await _labelRepository.DeleteAsync(label, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
