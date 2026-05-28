using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a user creates a new chat session via <c>CreateChatSessionCommand</c>.
/// Issue #3483: Chat Session Persistence Service.
///
/// BE-3 #1590 H2: registered in <see cref="Api.Infrastructure.DomainEventLog.EventTypeRegistry"/>
/// with alias <c>"chat.session.created"</c> (matches real command name, not fictional CreateChatThreadCommand).
///
/// <para><b>Payload fields (H2 spec):</b>
/// <list type="bullet">
///   <item><c>GameId</c> — game the session belongs to (always present)</item>
///   <item><c>GameName</c> — display name resolved via ISharedGameRepository (null when game not found)</item>
///   <item><c>AgentId</c> — custom agent definition id (null for system-agent or bare chat)</item>
///   <item><c>AgentName</c> — display name copied from aggregate (null when no agent)</item>
/// </list>
/// Required so the Task 8 activity rail can show title-meaningful chat items without a join.</para>
///
/// <para><b>Mapper conventions (DomainEventLogMapper):</b>
/// <list type="bullet">
///   <item><c>UserId</c> → <c>domain_event_logs.user_id</c> (reflection lookup)</item>
///   <item><c>AggregateId</c> → <c>domain_event_logs.aggregate_id</c> (reflection lookup, aliases SessionId)</item>
///   <item>Class name minus "Event" suffix → <c>domain_event_logs.aggregate_type = "ChatSessionCreated"</c></item>
///   <item>Payload serialized with <c>JsonNamingPolicy.CamelCase</c></item>
/// </list></para>
/// </summary>
internal sealed class ChatSessionCreatedEvent : DomainEventBase
{
    /// <summary>The new chat session's id. Named <c>AggregateId</c> so
    /// <see cref="Api.Infrastructure.DomainEventLog.DomainEventLogMapper"/> reflection
    /// populates <c>domain_event_logs.aggregate_id</c> without a custom mapper override.
    /// </summary>
    public Guid AggregateId { get; }

    /// <summary>Preserved for backward-compat with existing callers (aliases AggregateId).</summary>
    public Guid SessionId => AggregateId;

    /// <summary>User who triggered the creation. Reflected to <c>domain_event_logs.user_id</c>.</summary>
    public Guid UserId { get; }

    /// <summary>The game associated with this chat session.</summary>
    public Guid GameId { get; }

    /// <summary>
    /// Display name of the game, resolved at creation time via ISharedGameRepository.
    /// Null when the game is not found in the catalog (edge case: deleted / pending import).
    /// BE-3 #1590 H2: required for activity rail title rendering without a join.
    /// </summary>
    public string? GameName { get; }

    /// <summary>
    /// Id of the custom AgentDefinition used for this session.
    /// Null when a system agent type (auto/tutor/arbitro/decisore) is used, or for bare chat.
    /// </summary>
    public Guid? AgentId { get; }

    /// <summary>
    /// Display name of the agent. Copied from ChatSession.AgentName at creation time.
    /// Null when no agent is configured.
    /// </summary>
    public string? AgentName { get; }

    public ChatSessionCreatedEvent(
        Guid sessionId,
        Guid userId,
        Guid gameId,
        string? gameName,
        Guid? agentId,
        string? agentName)
    {
        AggregateId = sessionId;
        UserId = userId;
        GameId = gameId;
        GameName = gameName;
        AgentId = agentId;
        AgentName = agentName;
    }
}
