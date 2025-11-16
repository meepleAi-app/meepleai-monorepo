using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve simple comments for a RuleSpec version (legacy endpoint compatibility).
/// Returns flat list without threading.
/// </summary>
public record GetSimpleRuleCommentsQuery(
    string GameId,
    string Version
) : IRequest<RuleSpecCommentsResponse>;
