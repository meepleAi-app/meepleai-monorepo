using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to create a new rule comment with threading support.
/// Supports inline comments on specific lines and @mention functionality.
/// </summary>
internal record CreateRuleCommentCommand(
    string GameId,
    string Version,
    int? LineNumber,
    string CommentText,
    Guid UserId
) : IRequest<RuleCommentDto>;
