using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to mark a rule comment as resolved.
/// Optionally resolves all nested replies.
/// </summary>
public record ResolveRuleCommentCommand(
    Guid CommentId,
    Guid ResolvedByUserId,
    bool IsAdmin = false,
    bool ResolveReplies = false
) : IRequest<RuleCommentDto>;
