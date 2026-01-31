using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update an existing rule comment.
/// Only the comment author can update their comment.
/// </summary>
internal record UpdateRuleCommentCommand(
    Guid CommentId,
    string CommentText,
    Guid UserId
) : IRequest<RuleCommentDto>;
