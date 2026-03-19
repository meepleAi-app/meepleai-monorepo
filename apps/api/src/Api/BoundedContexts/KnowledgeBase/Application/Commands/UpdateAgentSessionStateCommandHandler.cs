using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for UpdateAgentSessionStateCommand.
/// Updates the current game state of an agent session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal sealed class UpdateAgentSessionStateCommandHandler : IRequestHandler<UpdateAgentSessionStateCommand>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAgentSessionStateCommandHandler> _logger;

    public UpdateAgentSessionStateCommandHandler(
        IAgentSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateAgentSessionStateCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        UpdateAgentSessionStateCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Updating game state for agent session {SessionId}",
            request.AgentSessionId);

        // Get agent session
        var agentSession = await _sessionRepository
            .GetByIdAsync(request.AgentSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentSession == null)
        {
            throw new NotFoundException("AgentSession", request.AgentSessionId.ToString());
        }

        // Parse and validate new game state
        var newGameState = GameState.FromJson(request.GameStateJson);

        // Update game state (domain handles IsActive validation)
        agentSession.UpdateGameState(newGameState);

        // Persist
        await _sessionRepository.UpdateAsync(agentSession, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Game state updated successfully for agent session {SessionId}",
            request.AgentSessionId);
    }
}
