using Api.Models;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to compute the difference between two RuleSpec versions.
/// Returns a detailed diff with change summary and field-level changes.
/// </summary>
public record ComputeRuleSpecDiffQuery(
    Guid GameId,
    string? FromVersion,
    string? ToVersion
) : IRequest<RuleSpecDiff>;
