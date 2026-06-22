using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a user creates a new agent via <c>CreateUserAgentCommand</c>
/// (user-facing flow — quick-create / POST /agents/user).
///
/// NOT raised for the admin/AI-Lab path (<c>CreateAgentDefinitionCommand</c>).
/// Decision H1 from BE-3 spec panel #1590.
///
/// Registered in <see cref="Api.Infrastructure.DomainEventLog.EventTypeRegistry"/>
/// with alias <c>"agent.created"</c>.
///
/// <para><b>Mapper conventions (DomainEventLogMapper):</b>
/// <list type="bullet">
///   <item><c>UserId</c> → <c>domain_event_logs.user_id</c> (reflection lookup)</item>
///   <item><c>AggregateId</c> → <c>domain_event_logs.aggregate_id</c> (reflection lookup)</item>
///   <item>Class name minus "Event" suffix → <c>domain_event_logs.aggregate_type = "AgentCreated"</c></item>
///   <item>Payload serialized with <c>JsonNamingPolicy.CamelCase</c></item>
/// </list></para>
/// </summary>
internal sealed class AgentCreatedEvent : DomainEventBase
{
    /// <summary>
    /// Logical FK to <c>agent_definitions.id</c>. Named <c>AggregateId</c> so
    /// <see cref="Api.Infrastructure.DomainEventLog.DomainEventLogMapper"/> reflection
    /// populates <c>domain_event_logs.aggregate_id</c> without a custom mapper override.
    /// </summary>
    public Guid AggregateId { get; }

    /// <summary>User who triggered the creation. Reflected to <c>domain_event_logs.user_id</c>.</summary>
    public Guid UserId { get; }

    /// <summary>Agent type value (e.g. "Tutor", "RAG").</summary>
    public string AgentType { get; }

    /// <summary>Whether the agent was immediately activated (always true for user flow).</summary>
    public bool IsActive { get; }

    /// <summary>Optional SharedGame the agent is linked to.</summary>
    public Guid? GameId { get; }

    /// <summary>Resolved game name at creation time. Null if no game linked.</summary>
    public string? GameName { get; }

    /// <summary>Resolved agent name (auto-derived or caller-supplied).</summary>
    public string AgentName { get; }

    /// <param name="aggregateId">The new agent definition id (<c>agent_definitions.id</c>).</param>
    /// <param name="userId">The user who created the agent.</param>
    /// <param name="agentType">Agent type value string.</param>
    /// <param name="isActive">Whether the agent was activated on creation.</param>
    /// <param name="gameId">Optional game the agent is linked to.</param>
    /// <param name="gameName">Resolved game name (non-null when <paramref name="gameId"/> is set).</param>
    /// <param name="agentName">Resolved agent name.</param>
    public AgentCreatedEvent(
        Guid aggregateId,
        Guid userId,
        string agentType,
        bool isActive,
        Guid? gameId,
        string? gameName,
        string agentName)
    {
        AggregateId = aggregateId;
        UserId = userId;
        AgentType = agentType;
        IsActive = isActive;
        GameId = gameId;
        GameName = gameName;
        AgentName = agentName;
    }
}
