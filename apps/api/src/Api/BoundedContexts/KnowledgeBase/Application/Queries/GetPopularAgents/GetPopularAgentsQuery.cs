using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPopularAgents;

/// <summary>
/// Query for the most popular agents in the catalog
/// (Wave 3 Phase 1, PR #732 §4.3.3 / Issue #805).
/// </summary>
/// <param name="Limit">Number of agents to return. Validator clamps to [1, 50]; default 10.</param>
internal sealed record GetPopularAgentsQuery(int Limit = 10) : IQuery<IReadOnlyList<PopularAgentDto>>;
