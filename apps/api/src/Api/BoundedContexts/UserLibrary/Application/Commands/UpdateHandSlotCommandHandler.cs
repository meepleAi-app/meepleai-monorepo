using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for <see cref="UpdateHandSlotCommand"/>.
/// Upserts the slot and persists changes via the unit of work.
/// </summary>
internal class UpdateHandSlotCommandHandler : ICommandHandler<UpdateHandSlotCommand, UserHandSlotDto>
{
    private readonly IUserHandRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateHandSlotCommandHandler(IUserHandRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserHandSlotDto> Handle(UpdateHandSlotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _repo.UpsertSlotAsync(
            command.UserId,
            command.SlotType,
            command.EntityId,
            command.EntityType,
            command.EntityLabel,
            command.EntityImageUrl,
            cancellationToken
        ).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserHandSlotDto(
            command.SlotType,
            command.EntityId,
            command.EntityType,
            command.EntityLabel,
            command.EntityImageUrl,
            DateTimeOffset.UtcNow.ToString("o")
        );
    }
}
