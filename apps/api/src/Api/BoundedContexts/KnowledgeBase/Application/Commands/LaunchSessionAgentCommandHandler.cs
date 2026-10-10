using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for LaunchSessionAgentCommand.
/// Creates a new AgentSession for a game session.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal sealed class LaunchSessionAgentCommandHandler : IRequestHandler<LaunchSessionAgentCommand, Guid>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LaunchSessionAgentCommandHandler> _logger;

    public LaunchSessionAgentCommandHandler(
        IAgentSessionRepository sessionRepository,
        IAgentDefinitionRepository agentDefinitionRepository,
        IUnitOfWork unitOfWork,
        ILogger<LaunchSessionAgentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        LaunchSessionAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Launching agent session for GameSession {GameSessionId}, AgentDefinition {AgentDefinitionId}",
            request.GameSessionId,
            request.AgentDefinitionId);

        // Check if an active session already exists
        var hasActiveSession = await _sessionRepository
            .HasActiveSessionAsync(request.GameSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (hasActiveSession)
        {
            throw new ConflictException(
                $"An active agent session already exists for GameSession {request.GameSessionId}");
        }

        var agentDefinitionId = request.AgentDefinitionId
            ?? await ResolveSystemAgentIdAsync(cancellationToken).ConfigureAwait(false);

        // C1 fix: empty/whitespace InitialGameStateJson means "use server default".
        // GameState.FromJson('{}') throws because ActivePlayer == Guid.Empty;
        // GameState.Initial(UserId) produces a valid baseline state.
        var initialGameState = string.IsNullOrWhiteSpace(request.InitialGameStateJson)
            ? GameState.Initial(request.UserId)
            : GameState.FromJson(request.InitialGameStateJson);

        // Create agent session
        var agentSession = new AgentSession(
            id: Guid.NewGuid(),
            agentDefinitionId: agentDefinitionId,
            gameSessionId: request.GameSessionId,
            userId: request.UserId,
            gameId: request.GameId,
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

    /// <summary>
    /// Issue #4154 (ADR-095 fetta 0b): senza un id esplicito, il lancio usa l'agente di sistema
    /// (ADR-094: uno solo, configurato dall'admin, non legato a un gioco). Il client non sceglie
    /// piu` da un elenco. Se l'admin non lo ha attivato, l'assistente non e` disponibile: lo stesso
    /// effetto che la regola V2 del validator dava per un id esplicito, ma con un codice che il
    /// client puo` riconoscere invece di una 422 generica.
    /// </summary>
    private async Task<Guid> ResolveSystemAgentIdAsync(CancellationToken cancellationToken)
    {
        var definitions = await _agentDefinitionRepository
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        var systemAgent = definitions
            .Where(d => d.IsSystemDefined)
            .OrderBy(d => d.CreatedAt)
            .FirstOrDefault();

        if (systemAgent is null)
        {
            throw new ConflictException(
                SystemAgentUnavailableCode,
                "L'assistente non è disponibile: non è configurato nessun agente di sistema.");
        }

        if (!systemAgent.IsActive)
        {
            throw new ConflictException(
                SystemAgentUnavailableCode,
                "L'assistente non è disponibile: l'agente di sistema non è attivo.");
        }

        return systemAgent.Id;
    }

    internal const string SystemAgentUnavailableCode = "system_agent_unavailable";
}
