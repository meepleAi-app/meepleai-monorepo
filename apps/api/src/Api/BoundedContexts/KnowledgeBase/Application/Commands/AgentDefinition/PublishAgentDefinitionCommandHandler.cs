using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Handler for PublishAgentDefinitionCommand.
/// Transitions an agent definition from Testing to Published status.
/// </summary>
internal sealed class PublishAgentDefinitionCommandHandler
    : IRequestHandler<PublishAgentDefinitionCommand>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PublishAgentDefinitionCommandHandler> _logger;

    public PublishAgentDefinitionCommandHandler(
        IAgentDefinitionRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<PublishAgentDefinitionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        PublishAgentDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agentDefinition = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition == null)
            throw new NotFoundException($"AgentDefinition {request.Id} not found");

        agentDefinition.Publish();

        await _repository.UpdateAsync(agentDefinition, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "AgentDefinition {Id} published",
            request.Id);
    }
}
