using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to mark a rule comment as unresolved.
/// Optionally unresolves the parent comment.
/// </summary>
internal record UnresolveRuleCommentCommand(
    Guid CommentId,
    Guid UserId,
    bool IsAdmin = false,
    bool UnresolveParent = false
) : IRequest<RuleCommentDto>;
