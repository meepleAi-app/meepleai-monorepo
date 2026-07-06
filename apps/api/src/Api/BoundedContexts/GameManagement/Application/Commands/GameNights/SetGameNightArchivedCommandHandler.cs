using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Archives / restores a game night. Organiser-only. The aggregate rejects archiving a
/// non-Completed night (Conflict).
/// </summary>
internal sealed class SetGameNightArchivedCommandHandler : ICommandHandler<SetGameNightArchivedCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetGameNightArchivedCommandHandler(
        IGameNightEventRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(SetGameNightArchivedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.UserId)
            throw new ForbiddenException("Only the organizer can archive this game night.");

        if (command.Archived)
            gameNight.Archive();
        else
            gameNight.Unarchive();

        await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
