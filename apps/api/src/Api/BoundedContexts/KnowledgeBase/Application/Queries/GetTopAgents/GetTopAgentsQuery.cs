using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;

/// <summary>
/// Query to retrieve the most-used agent definitions, ranked by distinct user count.
/// Used by the Discover dashboard "Top Agents" widget.
/// Approach A: aggregates AgentSessionEntity (proxy for install count).
/// Issue #728.
/// </summary>
internal sealed record GetTopAgentsQuery(int Limit) : IQuery<IReadOnlyList<TopAgentDto>>;
