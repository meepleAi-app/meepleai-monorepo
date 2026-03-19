using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for UpdateAgentSessionConfigCommand.
/// Updates the runtime configuration of an active agent session.
/// Issue #3253 (BACK-AGT-002): PATCH Endpoint - Update Agent Runtime Config.
/// </summary>
internal sealed class UpdateAgentSessionConfigCommandHandler : IRequestHandler<UpdateAgentSessionConfigCommand>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAgentSessionConfigCommandHandler> _logger;

    public UpdateAgentSessionConfigCommandHandler(
        IAgentSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateAgentSessionConfigCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        UpdateAgentSessionConfigCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Updating config for agent session {SessionId}",
            request.AgentSessionId);

        // Get agent session
        var agentSession = await _sessionRepository
            .GetByIdAsync(request.AgentSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentSession == null)
        {
            throw new NotFoundException("AgentSession", request.AgentSessionId.ToString());
        }

        // Create and validate new config
        var newConfig = AgentConfig.Create(
            request.ModelType,
            request.Temperature,
            request.MaxTokens,
            request.RagStrategy,
            request.RagParams
        );

        // Update config (domain handles IsActive validation)
        agentSession.UpdateConfig(newConfig);

        // Persist
        await _sessionRepository.UpdateAsync(agentSession, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Config updated successfully for agent session {SessionId}",
            request.AgentSessionId);
    }
}
