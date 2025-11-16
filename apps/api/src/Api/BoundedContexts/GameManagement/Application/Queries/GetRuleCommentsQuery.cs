using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve all comments for a RuleSpec version.
/// Returns threaded comments with optional filtering by resolution status.
/// </summary>
public record GetRuleCommentsQuery(
    string GameId,
    string Version,
    bool IncludeResolved = true
) : IRequest<IReadOnlyList<RuleCommentDto>>;
