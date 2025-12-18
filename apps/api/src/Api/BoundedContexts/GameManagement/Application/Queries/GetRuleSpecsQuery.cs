using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve rule specifications for a game.
/// </summary>
internal record GetRuleSpecsQuery(
    Guid GameId
) : IQuery<IReadOnlyList<RuleSpecDto>>;
