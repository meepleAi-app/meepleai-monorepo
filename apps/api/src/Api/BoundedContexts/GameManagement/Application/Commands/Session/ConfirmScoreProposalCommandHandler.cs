using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Hubs;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Handles host confirmation of a score proposal. Records the score and broadcasts to all participants.
/// E3-3: Score Proposal Flow.
/// </summary>
internal sealed class ConfirmScoreProposalCommandHandler : IRequestHandler<ConfirmScoreProposalCommand>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<ConfirmScoreProposalCommandHandler> _logger;

    public ConfirmScoreProposalCommandHandler(
        MeepleAiDbContext dbContext,
        ILiveSessionRepository sessionRepository,
        IHubContext<GameStateHub> hubContext,
        ILogger<ConfirmScoreProposalCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ConfirmScoreProposalCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Verify session exists via EF entity for ownership check
        var sessionEntity = await _dbContext.LiveGameSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Only the host can confirm scores
        if (sessionEntity.CreatedByUserId != request.ConfirmingUserId)
            throw new ForbiddenException("Only the session host can confirm score proposals");

        // Load domain entity via repository for domain logic
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"LiveGameSession {request.SessionId} not found in repository");

        // Record score through domain method (validates status, player, dimension)
        session.RecordScore(request.TargetPlayerId, request.Round, request.Dimension, request.Value);

        // Persist changes in-memory (LiveSessionRepository uses ConcurrentDictionary).
        // Scores are batch-persisted to DB when session completes via LiveSessionCompletedEventHandler.
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // Broadcast confirmation to all session participants
        await _hubContext.Clients.Group($"session:{request.SessionId}")
            .SendAsync("ScoreConfirmed", new
            {
                sessionId = request.SessionId,
                targetPlayerId = request.TargetPlayerId,
                round = request.Round,
                dimension = request.Dimension,
                value = request.Value,
                confirmedBy = request.ConfirmingUserId,
                timestamp = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Score confirmed in session {SessionId} by host {HostUserId} for player {TargetPlayerId}: round={Round}, dimension={Dimension}, value={Value}",
            request.SessionId, request.ConfirmingUserId, request.TargetPlayerId, request.Round, request.Dimension, request.Value);
    }
}
