using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;

/// <summary>
/// Command to add a comment to a shared chat thread (via share link with comment role).
/// </summary>
/// <param name="Token">JWT share link token</param>
/// <param name="Content">Message content</param>
public sealed record AddCommentToSharedThreadCommand(
    string Token,
    string Content
) : IRequest<AddCommentToSharedThreadResult?>;

/// <summary>
/// Result of adding comment to shared thread.
/// </summary>
/// <param name="MessageId">Unique identifier for the created message</param>
/// <param name="Timestamp">Message creation timestamp</param>
public sealed record AddCommentToSharedThreadResult(
    Guid MessageId,
    DateTime Timestamp
);
