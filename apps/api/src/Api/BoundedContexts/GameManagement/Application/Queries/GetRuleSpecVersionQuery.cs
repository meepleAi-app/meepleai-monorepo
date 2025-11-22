using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve a specific version of a rule specification.
/// </summary>
public record GetRuleSpecVersionQuery(
    Guid GameId,
    string Version
) : IQuery<RuleSpecDto?>;
