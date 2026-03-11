using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for linking an AI agent to a shared game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// Issue #97: Also sets Agent.GameId so RAG search works for SharedGame-linked agents.
/// </summary>
internal sealed class LinkAgentToSharedGameCommandHandler : ICommandHandler<LinkAgentToSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IAgentRepository _agentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LinkAgentToSharedGameCommandHandler> _logger;

    public LinkAgentToSharedGameCommandHandler(
        ISharedGameRepository repository,
        IAgentRepository agentRepository,
        IUnitOfWork unitOfWork,
        ILogger<LinkAgentToSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(LinkAgentToSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Linking agent {AgentId} to shared game {GameId}",
            command.AgentId, command.GameId);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());

        // Call domain method (validates not already linked and raises event)
        game.LinkAgent(command.AgentId);

        // Issue #97: Also set Agent.GameId so RAG search can filter by game_id
        var agent = await _agentRepository.GetByIdAsync(command.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is not null)
        {
            agent.SetGameId(command.GameId);
            await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning(
                "Agent {AgentId} not found in KnowledgeBase when linking to shared game {GameId}. Agent.GameId not set.",
                command.AgentId, command.GameId);
        }

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent {AgentId} linked successfully to shared game {GameId}",
            command.AgentId, command.GameId);

        return Unit.Value;
    }
}
