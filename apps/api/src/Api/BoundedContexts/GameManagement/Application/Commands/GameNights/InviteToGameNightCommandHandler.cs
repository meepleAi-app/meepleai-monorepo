using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles inviting additional users to a published game night.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Uses NotFoundException, calls UpdateAsync.
/// </summary>
internal sealed class InviteToGameNightCommandHandler : ICommandHandler<InviteToGameNightCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public InviteToGameNightCommandHandler(
        IGameNightEventRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(InviteToGameNightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.UserId)
            throw new UnauthorizedAccessException("Only the organizer can invite users to a game night");

        try
        {
            gameNight.AddInvitees(command.InvitedUserIds);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
