using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Handler for StartTestingAgentDefinitionCommand.
/// Transitions an agent definition from Draft to Testing status.
/// </summary>
internal sealed class StartTestingAgentDefinitionCommandHandler
    : IRequestHandler<StartTestingAgentDefinitionCommand>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ILogger<StartTestingAgentDefinitionCommandHandler> _logger;

    public StartTestingAgentDefinitionCommandHandler(
        IAgentDefinitionRepository repository,
        ILogger<StartTestingAgentDefinitionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        StartTestingAgentDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agentDefinition = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition == null)
            throw new NotFoundException($"AgentDefinition {request.Id} not found");

        agentDefinition.StartTesting();

        await _repository.UpdateAsync(agentDefinition, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "AgentDefinition {Id} transitioned to Testing status",
            request.Id);
    }
}
