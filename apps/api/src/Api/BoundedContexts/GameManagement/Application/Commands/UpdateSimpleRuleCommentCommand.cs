using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update a simple rule comment (legacy endpoint compatibility).
/// </summary>
public record UpdateSimpleRuleCommentCommand(
    Guid CommentId,
    string CommentText,
    Guid UserId
) : IRequest<RuleSpecComment>;
