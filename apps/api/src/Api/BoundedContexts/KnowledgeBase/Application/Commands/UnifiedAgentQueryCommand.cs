using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Unified streaming command for the API Gateway endpoint.
/// Routes queries to the appropriate agent based on intent classification.
/// Issue #4338: Unified API Gateway - /api/v1/agents/query
/// </summary>
internal record UnifiedAgentQueryCommand(
    string Query,
    Guid UserId,
    Guid? GameId = null,
    Guid? ChatThreadId = null,
    Guid? PreferredAgentId = null
) : IStreamingQuery<RagStreamingEvent>;
