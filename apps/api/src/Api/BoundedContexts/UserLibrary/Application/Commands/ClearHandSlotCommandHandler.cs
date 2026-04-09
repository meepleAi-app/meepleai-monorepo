using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for <see cref="ClearHandSlotCommand"/>.
/// Removes the entity assignment from a hand slot and persists via the unit of work.
/// </summary>
internal class ClearHandSlotCommandHandler : ICommandHandler<ClearHandSlotCommand>
{
    private readonly IUserHandRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public ClearHandSlotCommandHandler(IUserHandRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(ClearHandSlotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _repo.ClearSlotAsync(command.UserId, command.SlotType, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
