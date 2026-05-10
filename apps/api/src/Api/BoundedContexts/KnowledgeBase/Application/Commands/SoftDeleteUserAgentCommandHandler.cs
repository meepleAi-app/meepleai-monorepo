using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="SoftDeleteUserAgentCommand"/>.
///
/// Cascade strategy:
///   1. Load agent (must exist, must not be system-defined, must not already be deleted)
///   2. Call agent.SoftDelete() — sets IsDeleted=true, deactivates
///   3. Load all active ChatThreads linked to the agent via AgentId
///   4. Call thread.CloseThread() on each — cascade close
///   5. Persist all changes in sequence (two separate saves: agent, then threads)
///
/// Design note: This handler crosses the ChatThread aggregate boundary intentionally.
/// Per DDD principles, the handler (Application layer) is the correct place for
/// cross-aggregate orchestration. The domain entity (AgentDefinition) does NOT reach
/// into ChatThread directly — the cascade is choreographed here.
///
/// Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade.
/// </summary>
internal sealed class SoftDeleteUserAgentCommandHandler
    : IRequestHandler<SoftDeleteUserAgentCommand>
{
    private readonly IAgentDefinitionRepository _agentRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SoftDeleteUserAgentCommandHandler> _logger;

    public SoftDeleteUserAgentCommandHandler(
        IAgentDefinitionRepository agentRepository,
        IChatThreadRepository chatThreadRepository,
        IUnitOfWork unitOfWork,
        ILogger<SoftDeleteUserAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SoftDeleteUserAgentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load agent — global EF query filter excludes already-deleted agents
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
            throw new NotFoundException($"AgentDefinition {request.AgentId} not found");

        // 2. Guard: system-defined agents are immutable
        if (agent.IsSystemDefined)
            throw new SystemAgentProtectedException(agent.Id);

        // 3. Soft-delete the agent aggregate
        agent.SoftDelete();
        await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "AgentDefinition {AgentId} soft-deleted by user {UserId}",
            request.AgentId, request.UserId);

        // 4. Cascade: close all active ChatThreads associated with this agent
        //    (Cross-aggregate orchestration belongs in the Application layer per DDD)
        var activeThreads = await _chatThreadRepository
            .FindActiveByAgentIdAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var thread in activeThreads)
        {
            thread.CloseThread();
        }

        // 5. Persist cascaded thread closures
        foreach (var thread in activeThreads)
        {
            await _chatThreadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        }

        // ChatThreadRepository.UpdateAsync uses Unit-of-Work pattern (no auto SaveChanges).
        // Trigger persistence explicitly here.
        if (activeThreads.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (activeThreads.Count > 0)
        {
            _logger.LogInformation(
                "Cascade: closed {ThreadCount} active ChatThread(s) linked to AgentDefinition {AgentId}",
                activeThreads.Count, request.AgentId);
        }
    }
}
