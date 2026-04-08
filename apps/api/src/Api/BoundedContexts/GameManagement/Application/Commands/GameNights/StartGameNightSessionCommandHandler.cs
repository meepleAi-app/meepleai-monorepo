using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles starting a game session within a game night event.
/// Cross-BC communication: dispatches CreateSessionCommand to SessionTracking BC via MediatR,
/// then links the resulting session to the GameNightEvent aggregate.
///
/// If the command provides no participants, the handler auto-seeds the organizer
/// (looked up via Authentication.GetUserByIdQuery) as the sole owner participant.
/// </summary>
internal sealed class StartGameNightSessionCommandHandler : ICommandHandler<StartGameNightSessionCommand, StartGameNightSessionResult>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoSaveSchedulerService _autoSaveScheduler;

    public StartGameNightSessionCommandHandler(
        IGameNightEventRepository repository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        IAutoSaveSchedulerService autoSaveScheduler)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _autoSaveScheduler = autoSaveScheduler ?? throw new ArgumentNullException(nameof(autoSaveScheduler));
    }

    public async Task<StartGameNightSessionResult> Handle(
        StartGameNightSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.UserId)
            throw new ForbiddenException("Only the organizer can start sessions.");

        // Build participant list: auto-seed organizer when command provides none.
        var participants = await BuildParticipantsAsync(command, cancellationToken).ConfigureAwait(false);

        // Cross-BC: create Session via MediatR dispatch to SessionTracking
        var createResult = await _mediator.Send(new CreateSessionCommand(
            command.UserId,
            command.GameId,
            "GameSpecific",
            DateTime.UtcNow,
            null,
            participants), cancellationToken).ConfigureAwait(false);

        // Link the new session to the GameNight aggregate and start it
        try
        {
            var gns = gameNight.AddSession(createResult.SessionId, command.GameId, command.GameTitle);
            gameNight.StartCurrentSession();

            await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _autoSaveScheduler.RegisterAsync(createResult.SessionId, cancellationToken).ConfigureAwait(false);

            return new StartGameNightSessionResult(
                createResult.SessionId, gns.Id, createResult.SessionCode, gns.PlayOrder);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }
    }

    private async Task<List<ParticipantDto>> BuildParticipantsAsync(
        StartGameNightSessionCommand command, CancellationToken cancellationToken)
    {
        if (command.Participants is not null && command.Participants.Count > 0)
        {
            if (!command.Participants.Any(p => p.IsOwner))
                throw new ConflictException("At least one participant must be the session owner.");
            return command.Participants.ToList();
        }

        // Auto-seed: look up organizer via Authentication BC (cross-BC MediatR dispatch)
        var organizer = await _mediator.Send(new GetUserByIdQuery(command.UserId), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.UserId.ToString());

        var displayName = !string.IsNullOrWhiteSpace(organizer.DisplayName)
            ? organizer.DisplayName
            : organizer.Email;

        return new List<ParticipantDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                DisplayName = displayName,
                IsOwner = true,
                JoinOrder = 0
            }
        };
    }
}
