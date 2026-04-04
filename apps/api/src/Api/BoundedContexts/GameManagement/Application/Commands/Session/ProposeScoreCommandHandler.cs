using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.Hubs;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Handles score proposals from participants. Notifies the host via SignalR.
/// E3-3: Score Proposal Flow.
/// </summary>
internal sealed class ProposeScoreCommandHandler : IRequestHandler<ProposeScoreCommand>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<ProposeScoreCommandHandler> _logger;

    public ProposeScoreCommandHandler(
        MeepleAiDbContext dbContext,
        IHubContext<GameStateHub> hubContext,
        ILogger<ProposeScoreCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ProposeScoreCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Verify session exists and is active
        var session = await _dbContext.LiveGameSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Status 2=InProgress, 3=Paused
        if (session.Status != 2 && session.Status != 3)
            throw new ConflictException($"Cannot propose scores for session in status {session.Status}. Must be InProgress or Paused.");

        // Verify proposing participant is in the session
        var participantExists = await _dbContext.SessionParticipants
            .AnyAsync(p => p.Id == request.ProposingParticipantId && p.SessionId == request.SessionId && p.LeftAt == null, cancellationToken)
            .ConfigureAwait(false);

        if (!participantExists)
            throw new NotFoundException($"Participant {request.ProposingParticipantId} not found in session {request.SessionId}");

        // Send SignalR notification to host sub-group
        var proposalId = Guid.NewGuid();
        await _hubContext.Clients.Group($"session:{request.SessionId}:host")
            .SendAsync("ScoreProposed", new
            {
                proposalId,
                sessionId = request.SessionId,
                targetPlayerId = request.TargetPlayerId,
                round = request.Round,
                dimension = request.Dimension,
                value = request.Value,
                proposerName = request.ProposerName,
                timestamp = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Score proposed in session {SessionId} by participant {ParticipantId} for player {TargetPlayerId}: round={Round}, dimension={Dimension}, value={Value}",
            request.SessionId, request.ProposingParticipantId, request.TargetPlayerId, request.Round, request.Dimension, request.Value);
    }
}
