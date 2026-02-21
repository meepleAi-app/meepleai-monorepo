namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for a single admin chat session row.
/// Issue #4917: Admin chat history real data.
/// </summary>
/// <param name="Id">Chat thread UUID</param>
/// <param name="UserId">Owner user UUID</param>
/// <param name="UserName">User display name or email prefix</param>
/// <param name="Agent">Agent type used (tutor/arbitro/decisore/auto)</param>
/// <param name="MessageCount">Total messages in thread</param>
/// <param name="DurationSeconds">Duration from creation to last message</param>
/// <param name="Date">ISO-8601 timestamp of last message</param>
/// <param name="Preview">First 2 messages for table preview</param>
public record AdminChatSessionDto(
    string Id,
    string UserId,
    string UserName,
    string Agent,
    int MessageCount,
    int DurationSeconds,
    string Date,
    IReadOnlyList<AdminChatPreviewMessageDto> Preview
);

/// <summary>
/// Preview message for admin chat session.
/// </summary>
public record AdminChatPreviewMessageDto(
    string Role,
    string Content
);
