using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Query to get chat messages for a session.
/// Issue #4760
/// </summary>
public record GetSessionChatQuery(
    Guid SessionId,
    int? Limit,
    int? Offset
) : IRequest<SessionChatResultDto>;

/// <summary>
/// Result DTO for session chat query.
/// </summary>
public record SessionChatResultDto(
    IReadOnlyList<SessionChatMessageDto> Messages,
    int TotalCount
);

/// <summary>
/// DTO for a single chat message.
/// </summary>
public record SessionChatMessageDto(
    Guid Id,
    Guid SessionId,
    Guid? SenderId,
    string Content,
    string MessageType,
    int? TurnNumber,
    int SequenceNumber,
    string? AgentType,
    float? Confidence,
    string? CitationsJson,
    string? MentionsJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
