using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Agent aggregate root representing an AI agent with specific capabilities and strategy.
/// Agents are specialized knowledge retrieval and interpretation strategies within the KnowledgeBase context.
/// </summary>
/// <remarks>
/// Design Decision (ADR-004): Agents reside in KnowledgeBase context following "Bounded Context as Workspace" pattern.
/// They are domain entities that orchestrate VectorSearchDomainService and QualityTrackingDomainService.
/// </remarks>
internal sealed class Agent : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public AgentType Type { get; private set; }
    public AgentStrategy Strategy { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastInvokedAt { get; private set; }
    public int InvocationCount { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private Agent() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new agent with specified type and strategy.
    /// </summary>
    public Agent(
        Guid id,
        string name,
        AgentType type,
        AgentStrategy strategy,
        bool isActive = true) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name cannot be empty", nameof(name));

        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(strategy);

        Name = name.Trim();
        Type = type;
        Strategy = strategy;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        InvocationCount = 0;

        AddDomainEvent(new AgentCreatedEvent(id, type.Value, name));
    }

    /// <summary>
    /// Configures the agent with a new strategy.
    /// </summary>
    public void Configure(AgentStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        Strategy = strategy;

        AddDomainEvent(new AgentConfiguredEvent(Id, JsonSerializer.Serialize(strategy)));
    }

    /// <summary>
    /// Activates the agent for use.
    /// </summary>
    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;

        AddDomainEvent(new AgentActivatedEvent(Id));
    }

    /// <summary>
    /// Deactivates the agent.
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;

        AddDomainEvent(new AgentDeactivatedEvent(Id));
    }

    /// <summary>
    /// Records an agent invocation with token usage tracking.
    /// </summary>
    /// <remarks>
    /// This method should be called by the application layer after successful agent execution.
    /// The actual invocation logic (using VectorSearchDomainService, etc.) is handled by the command handler.
    /// Issue #1694: Now tracks actual token usage from LLM calls with cost calculation.
    /// </remarks>
    public void RecordInvocation(string input, TokenUsage tokenUsage)
    {
        if (!IsActive)
            throw new InvalidOperationException($"Cannot invoke inactive agent: {Name}");

        ArgumentNullException.ThrowIfNull(tokenUsage);

        LastInvokedAt = DateTime.UtcNow;
        InvocationCount++;

        // Emit domain event with token usage details for observability
        AddDomainEvent(new AgentInvokedEvent(
            Id,
            input,
            tokenUsage.TotalTokens,
            tokenUsage.EstimatedCost,
            tokenUsage.ModelId,
            tokenUsage.Provider));
    }

    /// <summary>
    /// Updates the agent name.
    /// </summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Agent name cannot be empty", nameof(newName));

        var trimmed = newName.Trim();
        if (trimmed.Length > 100)
            throw new ArgumentException("Agent name cannot exceed 100 characters", nameof(newName));

        Name = trimmed;
    }

    /// <summary>
    /// Checks if the agent has been invoked recently (within last 24 hours).
    /// </summary>
    public bool IsRecentlyUsed => LastInvokedAt.HasValue &&
                                   (DateTime.UtcNow - LastInvokedAt.Value).TotalHours < 24;

    /// <summary>
    /// Checks if the agent is idle (never used or not used in 7+ days).
    /// </summary>
    public bool IsIdle => !LastInvokedAt.HasValue ||
                          (DateTime.UtcNow - LastInvokedAt.Value).TotalDays >= 7;
}
