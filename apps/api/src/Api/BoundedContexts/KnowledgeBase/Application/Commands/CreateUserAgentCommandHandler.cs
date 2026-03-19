using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    private readonly IAgentRepository _agentRepository;
    private readonly MeepleAiDbContext _db;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<CreateUserAgentCommandHandler> _logger;

    public CreateUserAgentCommandHandler(
        IAgentRepository agentRepository,
        MeepleAiDbContext db,
        ITierEnforcementService tierEnforcementService,
        IRagAccessService ragAccessService,
        ILogger<CreateUserAgentCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto> Handle(
        CreateUserAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Ownership/RAG access check — user must own the game or game must be RAG-public
        var role = Enum.TryParse<UserRole>(request.UserRole, ignoreCase: true, out var parsedRole)
            ? parsedRole
            : UserRole.User;

        var canAccess = await _ragAccessService.CanAccessRagAsync(
            request.UserId, request.GameId, role, cancellationToken).ConfigureAwait(false);

        if (!canAccess)
        {
            throw new ForbiddenException("Devi possedere il gioco per creare un agente");
        }

        // E2-3: Tier enforcement — check agent creation quota via centralized service
        var canCreate = await _tierEnforcementService
            .CanPerformAsync(request.UserId, TierAction.CreateAgent, cancellationToken)
            .ConfigureAwait(false);

        if (!canCreate)
        {
            var usage = await _tierEnforcementService
                .GetUsageAsync(request.UserId, cancellationToken)
                .ConfigureAwait(false);

            throw new TierLimitExceededException(
                nameof(TierAction.CreateAgent),
                usage.Agents,
                usage.AgentsMax);
        }

        // Resolve game ID: input may be shared_games.Id from user library
        var resolvedGameId = await _agentRepository.ResolveGameIdAsync(request.GameId, cancellationToken).ConfigureAwait(false);
        if (resolvedGameId is null)
        {
            throw new NotFoundException(
                $"Game with ID {request.GameId} not found in the game catalog. " +
                "The game must exist in the game management system before an agent can be created.");
        }

        // Parse agent type
        var agentType = AgentType.Parse(request.AgentType);

        // Determine strategy based on tier
        var strategy = ResolveStrategy(request);

        // Generate name if not provided
        var name = !string.IsNullOrWhiteSpace(request.Name)
            ? request.Name.Trim()
            : $"{agentType.Value}-{resolvedGameId.Value.ToString()[..8]}";

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

        // Create agent aggregate (use resolved game ID, not the raw shared catalog ID)
        var agent = new Agent(
            id: Guid.NewGuid(),
            name: name,
            type: agentType,
            strategy: strategy,
            isActive: true,
            gameId: resolvedGameId.Value,
            createdByUserId: request.UserId
        );

        // Create default AgentConfiguration with optional document selection
        var documentIdsJson = request.DocumentIds is { Count: > 0 }
            ? JsonSerializer.Serialize(request.DocumentIds)
            : "[]";

        var defaultConfig = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            LlmProvider = AgentDefaults.DefaultLlmProvider,
            LlmModel = AgentDefaults.DefaultModel,
            AgentMode = 0, // Chat
            SelectedDocumentIdsJson = documentIdsJson,
            Temperature = AgentDefaults.DefaultTemperature,
            MaxTokens = AgentDefaults.DefaultMaxTokens,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.UserId
        };

        // Persist agent + config atomically — AddAsync calls SaveChangesAsync internally,
        // so we wrap both writes in a transaction to prevent orphaned agents.
        // Must use CreateExecutionStrategy() because NpgsqlRetryingExecutionStrategy is configured.
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async ct =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await _agentRepository.AddAsync(agent, ct).ConfigureAwait(false);
            _db.Set<AgentConfigurationEntity>().Add(defaultConfig);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);

        if (resolvedGameId.Value != request.GameId)
        {
            _logger.LogInformation(
                "User {UserId} created agent {AgentId} ({AgentType}) for game {ResolvedGameId} (resolved from shared catalog {OriginalGameId})",
                request.UserId, agent.Id, agentType.Value, resolvedGameId.Value, request.GameId);
        }
        else
        {
            _logger.LogInformation(
                "User {UserId} created agent {AgentId} ({AgentType}) for game {GameId}",
                request.UserId, agent.Id, agentType.Value, resolvedGameId.Value);
        }

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

}
