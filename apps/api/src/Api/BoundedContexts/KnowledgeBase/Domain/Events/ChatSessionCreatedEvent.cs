using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a user creates a new chat session via <c>CreateChatSessionCommand</c>.
/// Issue #3483: Chat Session Persistence Service.
///
/// BE-3 #1590 H2: registered in <see cref="Api.Infrastructure.DomainEventLog.EventTypeRegistry"/>
/// with alias <c>"chat.session.created"</c> (matches real command name, not fictional CreateChatThreadCommand).
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

    public ChatSessionCreatedEvent(Guid sessionId, Guid userId, Guid gameId)
    {
        AggregateId = sessionId;
        UserId = userId;
        GameId = gameId;
    }
}
