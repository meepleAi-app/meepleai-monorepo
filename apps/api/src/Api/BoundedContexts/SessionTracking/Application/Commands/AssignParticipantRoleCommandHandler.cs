using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles assigning a new role to a participant. Host-only action.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
public sealed class AssignParticipantRoleCommandHandler : IRequestHandler<AssignParticipantRoleCommand, AssignParticipantRoleResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;
    private readonly ILogger<AssignParticipantRoleCommandHandler> _logger;

    public AssignParticipantRoleCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService,
        ILogger<AssignParticipantRoleCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AssignParticipantRoleResult> Handle(
        AssignParticipantRoleCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        var participant = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session.");

        var previousRole = participant.Role;

        // Domain logic handles validation (finalized, last host, requester is host)
        session.AssignParticipantRole(request.ParticipantId, request.NewRole, request.RequesterId);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish SSE event
        var evt = new ParticipantRoleChangedEvent
        {
            SessionId = request.SessionId,
            ParticipantId = request.ParticipantId,
            DisplayName = participant.DisplayName,
            PreviousRole = previousRole,
            NewRole = request.NewRole,
            ChangedBy = request.RequesterId,
            Timestamp = DateTime.UtcNow
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Role changed for participant {ParticipantId} in session {SessionId}: {OldRole} → {NewRole} by {RequesterId}",
            request.ParticipantId, request.SessionId, previousRole, request.NewRole, request.RequesterId);

        return new AssignParticipantRoleResult(
            request.ParticipantId,
            participant.DisplayName,
            previousRole.ToString(),
            request.NewRole.ToString());
    }
}
