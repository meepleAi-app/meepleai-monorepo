using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.Hubs;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Session;

/// <summary>
/// Handles toggling AI agent access for a session participant.
/// E3-2: Only the session host can toggle access; participant is notified via SignalR.
/// </summary>
internal sealed class ToggleAgentAccessCommandHandler : IRequestHandler<ToggleAgentAccessCommand>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<ToggleAgentAccessCommandHandler> _logger;

    public ToggleAgentAccessCommandHandler(
        MeepleAiDbContext dbContext,
        IHubContext<GameStateHub> hubContext,
        ILogger<ToggleAgentAccessCommandHandler> logger)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(ToggleAgentAccessCommand request, CancellationToken cancellationToken)
    {
        var session = await _dbContext.LiveGameSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", request.SessionId.ToString());

        // Only the session creator (host) can toggle agent access
        if (session.CreatedByUserId != request.RequestingUserId)
            throw new ForbiddenException("Only the session host can toggle agent access");

        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.Id == request.ParticipantId && p.SessionId == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("SessionParticipant", request.ParticipantId.ToString());

        participant.AgentAccessEnabled = request.Enabled;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Notify the participant via SignalR
        await _hubContext.Clients
            .User(request.ParticipantId.ToString())
            .SendAsync("AgentAccessChanged", request.Enabled, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Agent access {Status} for participant {ParticipantId} in session {SessionId} by host {HostUserId}",
            request.Enabled ? "enabled" : "disabled",
            request.ParticipantId,
            request.SessionId,
            request.RequestingUserId);
    }
}
