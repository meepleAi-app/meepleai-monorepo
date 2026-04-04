using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for EndSessionAgentCommand.
/// Ends an agent session without deleting ChatLog history.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal sealed class EndSessionAgentCommandHandler : IRequestHandler<EndSessionAgentCommand>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EndSessionAgentCommandHandler> _logger;

    public EndSessionAgentCommandHandler(
        IAgentSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<EndSessionAgentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        EndSessionAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Ending agent session {SessionId}",
            request.AgentSessionId);

        // Get agent session
        var agentSession = await _sessionRepository
            .GetByIdAsync(request.AgentSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentSession == null)
        {
            throw new NotFoundException("AgentSession", request.AgentSessionId.ToString());
        }

        // End session (domain handles IsActive validation)
        agentSession.End();

        // Persist (ChatLog history is preserved, no cascade delete)
        await _sessionRepository.UpdateAsync(agentSession, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent session {SessionId} ended successfully. ChatLog history preserved.",
            request.AgentSessionId);
    }
}
