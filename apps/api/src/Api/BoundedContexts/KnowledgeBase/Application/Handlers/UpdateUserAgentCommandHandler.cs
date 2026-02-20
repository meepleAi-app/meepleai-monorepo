using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for UpdateUserAgentCommand.
/// Updates agent config with owner/admin authorization and tier enforcement.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
internal sealed class UpdateUserAgentCommandHandler : IRequestHandler<UpdateUserAgentCommand, AgentDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<UpdateUserAgentCommandHandler> _logger;

    public UpdateUserAgentCommandHandler(
        IAgentRepository agentRepository,
        ILogger<UpdateUserAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto> Handle(
        UpdateUserAgentCommand request,
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
            throw new ForbiddenException("You can only update your own agents");

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var trimmedName = request.Name.Trim();
            // Check per-user name uniqueness only if the name is actually changing
            if (!string.Equals(agent.Name, trimmedName, StringComparison.Ordinal))
            {
                var exists = await _agentRepository.ExistsByNameForUserAsync(
                    request.UserId, trimmedName, cancellationToken).ConfigureAwait(false);
                if (exists)
                    throw new ConflictException($"You already have an agent named '{trimmedName}'");
            }
            agent.Rename(trimmedName);
        }

        // Update strategy if provided with in-handler tier enforcement
        if (!string.IsNullOrWhiteSpace(request.StrategyName))
        {
            var tierLevel = GetTierLevel(request.UserTier);

            // Free tier: cannot change strategy (defense-in-depth, validator also checks)
            if (!isAdmin && tierLevel < 1)
                throw new ForbiddenException("Free tier users cannot configure strategy. Upgrade to Normal tier or higher.");

            // Normal tier: strategy name allowed but no custom parameters
            if (!isAdmin && tierLevel < 2 && request.StrategyParameters is { Count: > 0 })
                throw new ForbiddenException("Strategy parameters require Premium tier or higher.");

            var parameters = (isAdmin || tierLevel >= 2)
                ? request.StrategyParameters ?? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);

            var strategy = AgentStrategy.Custom(request.StrategyName, parameters);
            agent.Configure(strategy);
        }

        await _agentRepository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} updated agent {AgentId}",
            request.UserId, agent.Id);

        return CreateAgentCommandHandler.ToDto(agent);
    }

    private static int GetTierLevel(string tier)
    {
        return tier?.ToLowerInvariant() switch
        {
            "free" => 0,
            "normal" => 1,
            "premium" or "pro" => 2,
            "enterprise" => 3,
            _ => 0
        };
    }
}
