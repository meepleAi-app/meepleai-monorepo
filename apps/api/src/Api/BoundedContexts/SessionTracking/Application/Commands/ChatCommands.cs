using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to send a chat message in a session.
/// Issue #4760 - Shared Chat
/// </summary>
public record SendSessionChatMessageCommand(
    Guid SessionId,
    Guid SenderId,
    string Content,
    int? TurnNumber,
    string? MentionsJson
) : IRequest<SendChatMessageResult>;

public record SendChatMessageResult(Guid MessageId, int SequenceNumber);

/// <summary>
/// Command to send a system event message.
/// </summary>
public record SendSystemEventCommand(
    Guid SessionId,
    string Content,
    int? TurnNumber
) : IRequest<SendChatMessageResult>;

/// <summary>
/// Command to ask the RAG agent a question in session context.
/// </summary>
public record AskSessionAgentCommand(
    Guid SessionId,
    Guid SenderId,
    string Question,
    int? TurnNumber
) : IRequest<AskSessionAgentResult>;

public record AskSessionAgentResult(
    Guid MessageId,
    string Answer,
    string AgentType,
    float? Confidence,
    string? CitationsJson
);

/// <summary>
/// Command to delete a chat message.
/// </summary>
public record DeleteChatMessageCommand(
    Guid MessageId,
    Guid RequesterId
) : IRequest<Unit>;
