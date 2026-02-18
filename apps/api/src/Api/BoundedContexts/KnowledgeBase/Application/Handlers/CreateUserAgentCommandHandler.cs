using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for CreateUserAgentCommand.
/// Creates a user-owned agent with tier-aware defaults.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
internal sealed class CreateUserAgentCommandHandler : IRequestHandler<CreateUserAgentCommand, AgentDto>
{
    private const int MaxAutoSuffixAttempts = 5;

    // Max agents per user per tier
    private static readonly Dictionary<string, int> MaxAgentsPerTier = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"] = 3,
        ["normal"] = 10,
        ["premium"] = 50,
        ["pro"] = 50,
        ["enterprise"] = 200
    };

    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<CreateUserAgentCommandHandler> _logger;

    public CreateUserAgentCommandHandler(
        IAgentRepository agentRepository,
        ILogger<CreateUserAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto> Handle(
        CreateUserAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check agent quota per user
        var userAgents = await _agentRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        var maxAgents = GetMaxAgents(request.UserTier, request.UserRole);
        if (userAgents.Count >= maxAgents)
        {
            throw new ConflictException(
                $"Agent limit reached ({maxAgents}). Upgrade your tier for more agents.");
        }

        // Parse agent type
        var agentType = AgentType.Parse(request.AgentType);

        // Determine strategy based on tier
        var strategy = ResolveStrategy(request);

        // Generate name if not provided
        var name = !string.IsNullOrWhiteSpace(request.Name)
            ? request.Name.Trim()
            : $"{agentType.Value}-{request.GameId.ToString()[..8]}";

        // Check uniqueness scoped to this user (not global)
        var exists = await _agentRepository.ExistsByNameForUserAsync(request.UserId, name, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            // Auto-suffix with counter (max 5 attempts to prevent excessive queries)
            var baseName = name;
            var resolved = false;
            for (var i = 1; i <= MaxAutoSuffixAttempts; i++)
            {
                name = $"{baseName}-{i}";
                if (!await _agentRepository.ExistsByNameForUserAsync(request.UserId, name, cancellationToken).ConfigureAwait(false))
                {
                    resolved = true;
                    break;
                }
            }

            if (!resolved)
                throw new ConflictException($"Could not generate unique name for agent based on '{baseName}'");
        }

        // Create agent aggregate
        var agent = new Agent(
            id: Guid.NewGuid(),
            name: name,
            type: agentType,
            strategy: strategy,
            isActive: true,
            gameId: request.GameId,
            createdByUserId: request.UserId
        );

        // Persist
        await _agentRepository.AddAsync(agent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} created agent {AgentId} ({Type}) for game {GameId}",
            request.UserId, agent.Id, agentType.Value, request.GameId);

        return CreateAgentCommandHandler.ToDto(agent);
    }

    private static AgentStrategy ResolveStrategy(CreateUserAgentCommand request)
    {
        var isAdmin = string.Equals(request.UserRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(request.UserRole, "Editor", StringComparison.OrdinalIgnoreCase);
        var tierLevel = GetTierLevel(request.UserTier);

        // Premium/Admin: Full config
        if (isAdmin || tierLevel >= 2)
        {
            if (!string.IsNullOrWhiteSpace(request.StrategyName))
            {
                var parameters = request.StrategyParameters ?? new Dictionary<string, object>(StringComparer.Ordinal);
                return AgentStrategy.Custom(request.StrategyName, parameters);
            }
        }

        // Normal: Strategy name allowed, defaults for parameters
        if (tierLevel >= 1 && !string.IsNullOrWhiteSpace(request.StrategyName))
        {
            return AgentStrategy.Custom(request.StrategyName, new Dictionary<string, object>(StringComparer.Ordinal));
        }

        // Default: SingleModel
        return AgentStrategy.SingleModel();
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

    private static int GetMaxAgents(string tier, string role)
    {
        // Admin/Editor: unrestricted
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase))
            return int.MaxValue;

        return MaxAgentsPerTier.GetValueOrDefault(tier?.ToLowerInvariant() ?? "free", 3);
    }
}
