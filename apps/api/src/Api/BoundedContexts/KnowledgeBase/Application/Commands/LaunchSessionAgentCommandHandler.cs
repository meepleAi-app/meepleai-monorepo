using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for LaunchSessionAgentCommand.
/// Creates a new AgentSession for a game session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal sealed class LaunchSessionAgentCommandHandler : IRequestHandler<LaunchSessionAgentCommand, Guid>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LaunchSessionAgentCommandHandler> _logger;

    public LaunchSessionAgentCommandHandler(
        IAgentSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<LaunchSessionAgentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        LaunchSessionAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Launching agent session for GameSession {GameSessionId}, Typology {TypologyId}",
            request.GameSessionId,
            request.TypologyId);

        // Check if an active session already exists
        var hasActiveSession = await _sessionRepository
            .HasActiveSessionAsync(request.GameSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (hasActiveSession)
        {
            throw new ConflictException(
                $"An active agent session already exists for GameSession {request.GameSessionId}");
        }

        // Parse and validate initial game state
        var initialGameState = GameState.FromJson(request.InitialGameStateJson);

        // Create agent session
        var agentSession = new AgentSession(
            id: Guid.NewGuid(),
            agentId: request.AgentId,
            gameSessionId: request.GameSessionId,
            userId: request.UserId,
            gameId: request.GameId,
            typologyId: request.TypologyId,
            initialState: initialGameState
        );

        // Persist
        await _sessionRepository.AddAsync(agentSession, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent session {SessionId} launched successfully for GameSession {GameSessionId}",
            agentSession.Id,
            request.GameSessionId);

        return agentSession.Id;
    }
}
