using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles RSVP responses to game night invitations.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Uses NotFoundException, checks IsFull for Accept, raises domain event via aggregate.
/// </summary>
internal sealed class RespondToGameNightCommandHandler : ICommandHandler<RespondToGameNightCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RespondToGameNightCommandHandler(
        IGameNightEventRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(RespondToGameNightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        var rsvp = gameNight.GetRsvp(command.UserId)
            ?? throw new NotFoundException("GameNightRsvp", $"EventId={command.GameNightId}, UserId={command.UserId}");

        switch (command.Response)
        {
            case RsvpStatus.Accepted:
                if (gameNight.IsFull)
                    throw new ConflictException("This game night is full and cannot accept more players");
                rsvp.Accept();
                break;
            case RsvpStatus.Declined:
                rsvp.Decline();
                break;
            case RsvpStatus.Maybe:
                rsvp.SetMaybe();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), $"Invalid RSVP response: {command.Response}");
        }

        // Use domain event pattern for consistency — dispatched by UnitOfWork after SaveChanges.
        gameNight.AddRsvpReceivedEvent(command.UserId, command.Response, gameNight.OrganizerId);

        await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
