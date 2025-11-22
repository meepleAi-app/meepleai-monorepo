using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to delete a rule comment.
/// Only the comment author or an admin can delete a comment.
/// </summary>
public record DeleteRuleCommentCommand(
    Guid CommentId,
    Guid UserId,
    bool IsAdmin
) : IRequest<bool>;
