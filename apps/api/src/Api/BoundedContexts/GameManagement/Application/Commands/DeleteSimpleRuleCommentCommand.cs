using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to delete a simple rule comment (legacy endpoint compatibility).
/// </summary>
public record DeleteSimpleRuleCommentCommand(
    Guid CommentId,
    Guid UserId,
    bool IsAdmin
) : IRequest<bool>;
