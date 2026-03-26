using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for DeleteUserAgentCommand.
/// Soft-deletes (deactivates) agent with owner/admin authorization.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
internal sealed class DeleteUserAgentCommandHandler : IRequestHandler<DeleteUserAgentCommand, bool>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<DeleteUserAgentCommandHandler> _logger;

    public DeleteUserAgentCommandHandler(
        IAgentRepository agentRepository,
        ILogger<DeleteUserAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        DeleteUserAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent == null)
            throw new NotFoundException("Agent", request.AgentId.ToString());

        // Authorization: owner or admin
        var isAdmin = string.Equals(request.UserRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(request.UserRole, "Editor", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && agent.CreatedByUserId != request.UserId)
            throw new ForbiddenException("You can only delete your own agents");

        // Soft-delete: deactivate instead of hard delete to preserve data integrity
        agent.Deactivate();
        await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} deactivated agent {AgentId}",
            request.UserId, request.AgentId);

        return true;
    }
}
