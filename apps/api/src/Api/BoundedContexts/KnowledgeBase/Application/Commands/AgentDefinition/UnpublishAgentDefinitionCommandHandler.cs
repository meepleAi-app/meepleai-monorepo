using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Handler for UnpublishAgentDefinitionCommand.
/// Returns an agent definition to Draft status.
/// </summary>
internal sealed class UnpublishAgentDefinitionCommandHandler
    : IRequestHandler<UnpublishAgentDefinitionCommand>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ILogger<UnpublishAgentDefinitionCommandHandler> _logger;

    public UnpublishAgentDefinitionCommandHandler(
        IAgentDefinitionRepository repository,
        ILogger<UnpublishAgentDefinitionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        UnpublishAgentDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agentDefinition = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition == null)
            throw new NotFoundException($"AgentDefinition {request.Id} not found");

        agentDefinition.Unpublish();

        await _repository.UpdateAsync(agentDefinition, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "AgentDefinition {Id} unpublished (returned to Draft)",
            request.Id);
    }
}
