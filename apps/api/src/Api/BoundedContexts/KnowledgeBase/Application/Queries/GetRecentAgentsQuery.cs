using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get recently used agents for dashboard widget.
/// Issue #4126: API Integration.
/// </summary>
internal record GetRecentAgentsQuery(
    int Limit = 10
) : IQuery<IReadOnlyList<AgentDto>>;
