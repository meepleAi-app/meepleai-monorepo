using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to create a simple rule comment (legacy endpoint compatibility).
/// Uses AtomId instead of LineNumber.
/// </summary>
public record CreateSimpleRuleCommentCommand(
    string GameId,
    string Version,
    string? AtomId,
    string CommentText,
    Guid UserId
) : IRequest<RuleSpecComment>;
