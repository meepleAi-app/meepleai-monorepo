using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve a single rule specification for a game (latest version).
/// </summary>
internal record GetRuleSpecQuery(
    Guid GameId
) : IQuery<RuleSpecDto?>;
