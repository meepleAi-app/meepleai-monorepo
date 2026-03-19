using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Handler for DeleteAgentDefinitionCommand.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class DeleteAgentDefinitionCommandHandler
    : IRequestHandler<DeleteAgentDefinitionCommand>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ILogger<DeleteAgentDefinitionCommandHandler> _logger;

    public DeleteAgentDefinitionCommandHandler(
        IAgentDefinitionRepository repository,
        ILogger<DeleteAgentDefinitionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        DeleteAgentDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Verify exists
        var agentDefinition = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition == null)
            throw new NotFoundException($"AgentDefinition {request.Id} not found");

        // Delete
        await _repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deleted AgentDefinition {Id}",
            request.Id);
    }
}
