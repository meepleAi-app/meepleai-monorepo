using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="CreateUserAgentCommand"/>.
/// Issue #654 (Phase β.2): creates an AgentDefinition linked to a SharedGame
/// with sensible defaults, activates it so it appears in the user's agent list,
/// and returns an <see cref="AgentDto"/> with GameName resolved.
/// </summary>
/// <remarks>
/// MVP scope decisions:
/// <list type="bullet">
///   <item><c>DocumentIds</c> accepted but NOT linked (deferred to a follow-up).</item>
///   <item>No tier/quota validation (deferred to Issue #4771 agent slots).</item>
///   <item>Default config: <see cref="AgentDefinitionConfig.Default"/> (gpt-4 / 2048 tokens / 0.7 temp).</item>
///   <item>Default strategy: <c>AgentStrategy.HybridSearch()</c> when not provided.</item>
///   <item>Default name: <c>"{AgentType} for {GameName}"</c> if not provided.</item>
/// </list>
/// Persistence pattern (ADR-056): repository <c>AddAsync</c> mutates the
/// change-tracker only; this handler invokes <c>IUnitOfWork.SaveChangesAsync</c>
/// explicitly to persist.
/// </remarks>
internal sealed class CreateUserAgentCommandHandler
    : IRequestHandler<CreateUserAgentCommand, AgentDto>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly ILogger<CreateUserAgentCommandHandler> _logger;

    public CreateUserAgentCommandHandler(
        IAgentDefinitionRepository repository,
        ISharedGameRepository sharedGameRepository,
        ITierEnforcementService tierEnforcementService,
        IUnitOfWork unitOfWork,
        ILogger<CreateUserAgentCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto> Handle(CreateUserAgentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.AgentType))
            throw new ArgumentException("AgentType is required", nameof(request));
        if (request.GameId == Guid.Empty)
            throw new ArgumentException("GameId is required", nameof(request));

        // Issue #904: SG3 — Quota check: enforce agent slot limit for the user's subscription tier.
        // Uses TierAction.CreateAgent which maps to limits.MaxAgents in TierEnforcementService.
        var canCreate = await _tierEnforcementService
            .CanPerformAsync(request.UserId, TierAction.CreateAgent, cancellationToken)
            .ConfigureAwait(false);
        if (!canCreate)
        {
            var limits = await _tierEnforcementService
                .GetLimitsAsync(request.UserId, cancellationToken)
                .ConfigureAwait(false);
            throw new TierQuotaExceededException("AgentSlots", limits.MaxAgents);
        }

        // Resolve game name (also validates the game exists in the catalog).
        var gameNames = await _sharedGameRepository
            .GetNamesByIdsAsync(new[] { request.GameId }, cancellationToken)
            .ConfigureAwait(false);
        if (!gameNames.TryGetValue(request.GameId, out var gameName))
        {
            throw new InvalidOperationException($"SharedGame {request.GameId} not found");
        }

        // Parse and validate AgentType. Throws ArgumentException on unknown values.
        var agentType = AgentType.Parse(request.AgentType);

        // Default config (gpt-4 / 2048 tokens / 0.7 temperature) — caller may update later via PUT.
        var config = AgentDefinitionConfig.Default();

        // Resolve strategy: HybridSearch by default; otherwise Custom with caller params.
        var strategy = ResolveStrategy(request.StrategyName, request.StrategyParameters);

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{request.AgentType} for {gameName}"
            : request.Name!.Trim();

        var agent = Domain.Entities.AgentDefinition.Create(
            name: name,
            description: string.Empty,
            type: agentType,
            config: config,
            strategy: strategy);

        agent.SetGameId(request.GameId);
        // Activate so it shows up in the user's agent list immediately.
        if (!agent.IsActive) agent.Activate();

        await _repository.AddAsync(agent, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Record usage so CanPerformAsync reflects the new count on next call (Redis atomic counter).
        await _tierEnforcementService
            .RecordUsageAsync(request.UserId, TierAction.CreateAgent, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Created user-owned AgentDefinition {Id} '{Name}' linked to SharedGame {GameId} for user {UserId}",
            agent.Id,
            agent.Name,
            request.GameId,
            request.UserId);

        var recentThreshold = DateTime.UtcNow.AddHours(-24);
        var idleThreshold = DateTime.UtcNow.AddDays(-7);

        return new AgentDto(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type.Value,
            StrategyName: agent.Strategy.Name,
            StrategyParameters: agent.Strategy.Parameters,
            IsActive: agent.IsActive,
            CreatedAt: agent.CreatedAt,
            LastInvokedAt: agent.LastInvokedAt,
            InvocationCount: agent.InvocationCount,
            IsRecentlyUsed: agent.LastInvokedAt.HasValue && agent.LastInvokedAt.Value > recentThreshold,
            IsIdle: !agent.LastInvokedAt.HasValue || agent.LastInvokedAt.Value < idleThreshold,
            GameId: agent.GameId,
            GameName: gameName,
            CreatedByUserId: request.UserId
        );
    }

    private static AgentStrategy ResolveStrategy(
        string? name,
        IReadOnlyDictionary<string, object>? parameters)
    {
        if (string.IsNullOrWhiteSpace(name))
            return AgentStrategy.HybridSearch();

        // Mirror CreateAgentDefinitionCommandHandler.ResolveStrategy preset support,
        // then fall back to Custom for arbitrary names with caller-supplied parameters.
        return name switch
        {
            "RetrievalOnly" => AgentStrategy.RetrievalOnly(),
            "HybridSearch" => AgentStrategy.HybridSearch(),
            "SentenceWindowRAG" => AgentStrategy.SentenceWindowRAG(),
            "ColBERTReranking" => AgentStrategy.ColBERTReranking(),
            _ => AgentStrategy.Custom(
                name,
                parameters is null
                    ? null
                    : new Dictionary<string, object>(parameters, StringComparer.Ordinal))
        };
    }
}
