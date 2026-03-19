using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for UpdateAgentSessionTypologyCommand.
/// Updates the agent typology of an active agent session.
/// Issue #3252 (BACK-AGT-001): PATCH Endpoint - Update Agent Typology.
/// </summary>
internal sealed class UpdateAgentSessionTypologyCommandHandler : IRequestHandler<UpdateAgentSessionTypologyCommand>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentTypologyRepository _typologyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAgentSessionTypologyCommandHandler> _logger;

    public UpdateAgentSessionTypologyCommandHandler(
        IAgentSessionRepository sessionRepository,
        IAgentTypologyRepository typologyRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateAgentSessionTypologyCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _typologyRepository = typologyRepository ?? throw new ArgumentNullException(nameof(typologyRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        UpdateAgentSessionTypologyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Updating typology for agent session {SessionId} to {NewTypologyId}",
            request.AgentSessionId,
            request.NewTypologyId);

        // Get agent session
        var agentSession = await _sessionRepository
            .GetByIdAsync(request.AgentSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentSession == null)
        {
            throw new NotFoundException("AgentSession", request.AgentSessionId.ToString());
        }

        // Validate new typology exists and is approved
        var typology = await _typologyRepository
            .GetByIdAsync(request.NewTypologyId, cancellationToken)
            .ConfigureAwait(false);

        if (typology == null)
        {
            throw new NotFoundException("AgentTypology", request.NewTypologyId.ToString());
        }

        if (typology.Status != TypologyStatus.Approved)
        {
            throw new ValidationException($"Typology {request.NewTypologyId} is not approved");
        }

        // Update typology (domain handles IsActive validation)
        agentSession.UpdateTypology(request.NewTypologyId);

        // Persist
        await _sessionRepository.UpdateAsync(agentSession, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Typology updated successfully for agent session {SessionId} to {NewTypologyId}",
            request.AgentSessionId,
            request.NewTypologyId);
    }
}
