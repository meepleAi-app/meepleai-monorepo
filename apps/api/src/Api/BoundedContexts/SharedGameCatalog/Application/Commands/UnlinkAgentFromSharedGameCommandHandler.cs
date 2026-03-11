using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for unlinking an AI agent from a shared game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// Issue #97: Also clears Agent.GameId so stale game associations are removed.
/// </summary>
internal sealed class UnlinkAgentFromSharedGameCommandHandler : ICommandHandler<UnlinkAgentFromSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IAgentRepository _agentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnlinkAgentFromSharedGameCommandHandler> _logger;

    public UnlinkAgentFromSharedGameCommandHandler(
        ISharedGameRepository repository,
        IAgentRepository agentRepository,
        IUnitOfWork unitOfWork,
        ILogger<UnlinkAgentFromSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(UnlinkAgentFromSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Unlinking agent from shared game {GameId}",
            command.GameId);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());

        // Capture agent ID before unlinking (domain method clears it)
        var agentId = game.AgentDefinitionId;

        // Call domain method (validates agent is linked and raises event)
        game.UnlinkAgent();

        // Issue #97: Clear Agent.GameId so stale game associations are removed
        if (agentId.HasValue)
        {
            var agent = await _agentRepository.GetByIdAsync(agentId.Value, cancellationToken).ConfigureAwait(false);
            if (agent is not null)
            {
                agent.ClearGameId();
                await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning(
                    "Agent {AgentId} not found in KnowledgeBase when unlinking from shared game {GameId}. Agent.GameId not cleared.",
                    agentId.Value, command.GameId);
            }
        }

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent unlinked successfully from shared game {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
