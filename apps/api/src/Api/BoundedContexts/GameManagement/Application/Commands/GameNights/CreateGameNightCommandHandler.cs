using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNights;

/// <summary>
/// Handles creation of a new game night event.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Returns enriched DTO with OrganizerName.
/// </summary>
internal sealed class CreateGameNightCommandHandler : ICommandHandler<CreateGameNightCommand, Guid>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGameNightCommandHandler(
        IGameNightEventRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Guid> Handle(CreateGameNightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = GameNightEvent.Create(
            organizerId: command.UserId,
            title: command.Title,
            scheduledAt: command.ScheduledAt,
            description: command.Description,
            location: command.Location,
            maxPlayers: command.MaxPlayers,
            gameIds: command.GameIds);

        // Pre-create RSVP entries for invited users so PublishHandler sends invitations.
        if (command.InvitedUserIds is { Count: > 0 })
        {
            gameNight.PreInvite(command.InvitedUserIds);
        }

        await _repository.AddAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return gameNight.Id;
    }
}
