using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="UpdateUserAgentCommand"/>.
/// Issue #656: updates name and/or strategy of an existing AgentDefinition and returns
/// an <see cref="AgentDto"/> with GameName resolved (drift-fix lookup pattern from PR #662).
/// </summary>
/// <remarks>
/// Persistence pattern mirrors <c>CreateUserAgentCommandHandler</c>: repository's
/// <c>UpdateAsync</c> calls <c>DbContext.SaveChangesAsync</c> internally, so no
/// <c>IUnitOfWork</c> is required.
/// Returns <c>null</c> when the agent ID is not found (endpoint maps to 404).
/// Strategy resolution mirrors the preset-or-Custom pattern used by
/// <see cref="CreateUserAgentCommandHandler"/>.
/// </remarks>
internal sealed class UpdateUserAgentCommandHandler
    : IRequestHandler<UpdateUserAgentCommand, AgentDto?>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ILogger<UpdateUserAgentCommandHandler> _logger;

    public UpdateUserAgentCommandHandler(
        IAgentDefinitionRepository repository,
        ISharedGameRepository sharedGameRepository,
        ILogger<UpdateUserAgentCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDto?> Handle(UpdateUserAgentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await _repository.GetByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return null;
        }

        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.Name) &&
            !string.Equals(agent.Name, request.Name, StringComparison.Ordinal))
        {
            agent.UpdateNameAndDescription(request.Name, agent.Description);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.StrategyName))
        {
            var newStrategy = ResolveStrategy(request.StrategyName, request.StrategyParameters);
            agent.UpdateStrategy(newStrategy);
            changed = true;
        }

        if (changed)
        {
            await _repository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Updated user-owned AgentDefinition {Id} (Name='{Name}', Strategy='{Strategy}') for user {UserId}",
                agent.Id,
                agent.Name,
                agent.Strategy.Name,
                request.UserId);
        }

        // Map to AgentDto with GameName (drift-fix lookup PR #662 pattern).
        string? gameName = null;
        if (agent.GameId.HasValue)
        {
            var names = await _sharedGameRepository
                .GetNamesByIdsAsync(new[] { agent.GameId.Value }, cancellationToken)
                .ConfigureAwait(false);
            names.TryGetValue(agent.GameId.Value, out gameName);
        }

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
        // Mirror CreateUserAgentCommandHandler.ResolveStrategy preset support,
        // then fall back to Custom for arbitrary names with caller-supplied parameters.
        return name switch
        {
            "RetrievalOnly" => AgentStrategy.RetrievalOnly(),
            "HybridSearch" => AgentStrategy.HybridSearch(),
            "SentenceWindowRAG" => AgentStrategy.SentenceWindowRAG(),
            "ColBERTReranking" => AgentStrategy.ColBERTReranking(),
            _ => AgentStrategy.Custom(
                name!,
                parameters is null
                    ? null
                    : new Dictionary<string, object>(parameters, StringComparer.Ordinal))
        };
    }
}
