using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for UpdateAgentSessionDefinitionCommand.
/// Updates the agent definition of an active agent session.
/// </summary>
internal sealed class UpdateAgentSessionDefinitionCommandHandler : IRequestHandler<UpdateAgentSessionDefinitionCommand>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentDefinitionRepository _definitionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAgentSessionDefinitionCommandHandler> _logger;

    public UpdateAgentSessionDefinitionCommandHandler(
        IAgentSessionRepository sessionRepository,
        IAgentDefinitionRepository definitionRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateAgentSessionDefinitionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        UpdateAgentSessionDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Updating agent definition for session {SessionId} to {NewAgentDefinitionId}",
            request.AgentSessionId,
            request.NewAgentDefinitionId);

        var agentSession = await _sessionRepository
            .GetByIdAsync(request.AgentSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentSession == null)
            throw new NotFoundException("AgentSession", request.AgentSessionId.ToString());

        var definition = await _definitionRepository
            .GetByIdAsync(request.NewAgentDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        if (definition == null)
            throw new NotFoundException("AgentDefinition", request.NewAgentDefinitionId.ToString());

        if (!definition.IsActive)
            throw new ValidationException($"AgentDefinition {request.NewAgentDefinitionId} is not active");

        agentSession.UpdateAgentDefinition(request.NewAgentDefinitionId);

        await _sessionRepository.UpdateAsync(agentSession, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent definition updated for session {SessionId} to {NewAgentDefinitionId}",
            request.AgentSessionId,
            request.NewAgentDefinitionId);
    }
}
