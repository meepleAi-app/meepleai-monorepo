using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handles joining a session via the 6-character session code.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
public sealed class JoinSessionByCodeCommandHandler : IRequestHandler<JoinSessionByCodeCommand, JoinSessionByCodeResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;
    private readonly ILogger<JoinSessionByCodeCommandHandler> _logger;

    public JoinSessionByCodeCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService,
        ILogger<JoinSessionByCodeCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<JoinSessionByCodeResult> Handle(
        JoinSessionByCodeCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedCode = request.SessionCode.ToUpperInvariant();

        var session = await _sessionRepository.GetByCodeAsync(normalizedCode, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session with code '{normalizedCode}' not found.");

        // Cannot join finalized sessions
        if (session.Status == SessionStatus.Finalized)
            throw new ConflictException("Cannot join a finalized session.");

        // Check if user is already a participant
        var existing = session.Participants.FirstOrDefault(p => p.UserId == request.UserId);
        if (existing != null)
        {
            _logger.LogInformation(
                "User {UserId} is already a participant in session {SessionId}",
                request.UserId, session.Id);

            return new JoinSessionByCodeResult(
                session.Id,
                session.SessionCode,
                existing.Id,
                existing.DisplayName,
                existing.Role.ToString(),
                existing.JoinOrder);
        }

        // Enforce participant cap
        if (session.Participants.Count >= 20)
            throw new ConflictException("Maximum 20 participants allowed per session.");

        // Add participant
        var participantInfo = ParticipantInfo.Create(
            displayName: request.DisplayName,
            isOwner: false,
            joinOrder: session.Participants.Count + 1);

        session.AddParticipant(participantInfo, request.UserId);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var newParticipant = session.Participants.Last();

        // Publish SSE event
        var evt = new ParticipantAddedEvent
        {
            SessionId = session.Id,
            ParticipantId = newParticipant.Id,
            DisplayName = newParticipant.DisplayName,
            IsOwner = false,
            JoinOrder = newParticipant.JoinOrder,
            Timestamp = DateTime.UtcNow
        };
        await _syncService.PublishEventAsync(session.Id, evt, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} joined session {SessionId} via code '{Code}' as participant {ParticipantId}",
            request.UserId, session.Id, normalizedCode, newParticipant.Id);

        return new JoinSessionByCodeResult(
            session.Id,
            session.SessionCode,
            newParticipant.Id,
            newParticipant.DisplayName,
            newParticipant.Role.ToString(),
            newParticipant.JoinOrder);
    }
}
