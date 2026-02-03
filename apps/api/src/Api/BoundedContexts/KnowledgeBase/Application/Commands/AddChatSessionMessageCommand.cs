using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to add a message to a chat session.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal record AddChatSessionMessageCommand(
    Guid SessionId,
    string Role,
    string Content,
    Dictionary<string, object>? Metadata = null
) : IRequest<Guid>;
