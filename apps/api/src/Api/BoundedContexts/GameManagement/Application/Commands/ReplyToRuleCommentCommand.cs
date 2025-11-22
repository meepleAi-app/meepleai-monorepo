using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to reply to an existing rule comment, creating a threaded conversation.
/// </summary>
public record ReplyToRuleCommentCommand(
    Guid ParentCommentId,
    string CommentText,
    Guid UserId
) : IRequest<RuleCommentDto>;
