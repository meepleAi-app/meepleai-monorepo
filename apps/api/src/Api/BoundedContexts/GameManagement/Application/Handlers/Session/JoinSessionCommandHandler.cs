using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Session;

/// <summary>
/// Handles joining a session via invite PIN or link token.
/// E3-1: Supports registered users and anonymous guests with tier-based player limits.
/// </summary>
internal sealed class JoinSessionCommandHandler : IRequestHandler<JoinSessionCommand, JoinSessionResultDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ITierEnforcementService _tierService;

    public JoinSessionCommandHandler(MeepleAiDbContext dbContext, ITierEnforcementService tierService)
    {
        _dbContext = dbContext;
        _tierService = tierService;
    }

    public async Task<JoinSessionResultDto> Handle(JoinSessionCommand request, CancellationToken cancellationToken)
    {
        // Find invite by PIN or link token
        var normalizedToken = request.Token.ToUpperInvariant();
        var invite = await _dbContext.SessionInvites
            .FirstOrDefaultAsync(i =>
                i.Pin == normalizedToken ||
                i.LinkToken == request.Token,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Invalid invite code");

        if (invite.IsRevoked)
            throw new ConflictException("This invite has been revoked");

        if (invite.ExpiresAt <= DateTime.UtcNow)
            throw new ConflictException("This invite has expired");

        if (invite.CurrentUses >= invite.MaxUses)
            throw new ConflictException("This invite has reached its maximum uses");

        // Check session exists and is active
        var session = await _dbContext.LiveGameSessions
            .FirstOrDefaultAsync(s => s.Id == invite.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found");

        // Check max players via tier of the host
        var hostTierLimits = await _tierService.GetLimitsAsync(session.CreatedByUserId, cancellationToken)
            .ConfigureAwait(false);

        var currentParticipants = await _dbContext.SessionParticipants
            .CountAsync(p => p.SessionId == invite.SessionId && p.LeftAt == null, cancellationToken)
            .ConfigureAwait(false);

        if (currentParticipants >= hostTierLimits.MaxSessionPlayers)
            throw new ConflictException($"Session has reached the maximum of {hostTierLimits.MaxSessionPlayers} players");

        // Check if user already joined
        if (request.UserId.HasValue)
        {
            var alreadyJoined = await _dbContext.SessionParticipants
                .AnyAsync(p => p.SessionId == invite.SessionId && p.UserId == request.UserId && p.LeftAt == null, cancellationToken)
                .ConfigureAwait(false);

            if (alreadyJoined)
                throw new ConflictException("You have already joined this session");
        }

        // Create participant
        SessionParticipant participant;
        if (request.UserId.HasValue)
        {
            participant = SessionParticipant.CreateRegistered(invite.SessionId, request.UserId.Value, ParticipantRole.Player);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.GuestName))
                throw new SharedKernel.Domain.Exceptions.ValidationException("GuestName", "Guest name is required for anonymous join");

            participant = SessionParticipant.CreateGuest(invite.SessionId, request.GuestName, ParticipantRole.Player);
        }

        _dbContext.SessionParticipants.Add(new SessionParticipantEntity
        {
            Id = participant.Id,
            SessionId = participant.SessionId,
            UserId = participant.UserId,
            GuestName = participant.GuestName,
            Role = participant.Role.ToString(),
            AgentAccessEnabled = participant.AgentAccessEnabled,
            ConnectionToken = participant.ConnectionToken,
            JoinedAt = participant.JoinedAt,
            LeftAt = null
        });

        // Record invite use
        invite.CurrentUses++;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new JoinSessionResultDto(
            participant.Id,
            participant.SessionId,
            participant.ConnectionToken,
            participant.DisplayName,
            participant.Role.ToString());
    }
}
