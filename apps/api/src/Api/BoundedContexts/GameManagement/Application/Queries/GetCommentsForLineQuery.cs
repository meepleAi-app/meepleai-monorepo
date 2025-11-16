using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve all comments for a specific line in a RuleSpec version.
/// Returns top-level comments and their threaded replies.
/// </summary>
public record GetCommentsForLineQuery(
    string GameId,
    string Version,
    int LineNumber
) : IRequest<IReadOnlyList<RuleCommentDto>>;
