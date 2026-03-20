using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Handles creating a session invite (PIN + link token).
/// E3-1: Only the session host can create invites.
/// </summary>
internal sealed class CreateSessionInviteCommandHandler : IRequestHandler<CreateSessionInviteCommand, SessionInviteResultDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public CreateSessionInviteCommandHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SessionInviteResultDto> Handle(CreateSessionInviteCommand request, CancellationToken cancellationToken)
    {
        var session = await _dbContext.LiveGameSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Only the session creator (host) can create invites
        if (session.CreatedByUserId != request.UserId)
            throw new ForbiddenException("Only the session host can create invites");

        var invite = SessionInvite.Create(
            request.SessionId,
            request.UserId,
            request.MaxUses,
            request.ExpiryMinutes);

        _dbContext.SessionInvites.Add(new SessionInviteEntity
        {
            Id = invite.Id,
            SessionId = invite.SessionId,
            CreatedByUserId = invite.CreatedByUserId,
            Pin = invite.Pin,
            LinkToken = invite.LinkToken,
            MaxUses = invite.MaxUses,
            CurrentUses = invite.CurrentUses,
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt,
            IsRevoked = invite.IsRevoked
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SessionInviteResultDto(
            invite.Pin,
            invite.LinkToken,
            invite.ExpiresAt,
            invite.MaxUses);
    }
}
