using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetAgentStatusQuery"/> — returns an <see cref="AgentStatusDto"/> for the
/// user-facing <c>GET /api/v1/agents/{id}/status</c> route. Issue #648 (Phase γ.2).
/// </summary>
/// <remarks>
/// MVP readiness derived from <see cref="Domain.Entities.AgentDefinition"/> entity flags only:
/// <list type="bullet">
///   <item><description><c>IsActive</c>: from <c>agent.IsActive</c></description></item>
///   <item><description><c>HasConfiguration</c>: <c>true</c> when <c>agent.Strategy.Name</c> is non-empty</description></item>
///   <item><description><c>IsReady</c>: <c>IsActive AND HasConfiguration</c></description></item>
///   <item><description><c>RagStatus</c>: <c>"inactive"</c> | <c>"misconfigured"</c> | <c>"ready"</c></description></item>
/// </list>
/// <c>HasDocuments</c> precise count is deferred (would require a SelectedDocuments query
/// repository). Frontend contract satisfied with shape correctness; file a follow-up issue if
/// downstream consumers require precise counts.
/// </remarks>
internal sealed class GetAgentStatusQueryHandler
    : IRequestHandler<GetAgentStatusQuery, AgentStatusDto?>
{
    private readonly IAgentDefinitionRepository _repository;

    public GetAgentStatusQueryHandler(IAgentDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AgentStatusDto?> Handle(
        GetAgentStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await _repository
            .GetByIdAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent is null)
        {
            return null;
        }

        var hasConfiguration = !string.IsNullOrWhiteSpace(agent.Strategy.Name);
        const bool hasDocuments = false;
        const int documentCount = 0;

        var isReady = agent.IsActive && hasConfiguration;
        var ragStatus = !agent.IsActive
            ? "inactive"
            : !hasConfiguration
                ? "misconfigured"
                : "ready";

        string? blockingReason = null;
        if (!agent.IsActive)
        {
            blockingReason = "Agent is not active";
        }
        else if (!hasConfiguration)
        {
            blockingReason = "Agent strategy is not configured";
        }

        return new AgentStatusDto(
            AgentId: agent.Id,
            Name: agent.Name,
            IsActive: agent.IsActive,
            IsReady: isReady,
            HasConfiguration: hasConfiguration,
            HasDocuments: hasDocuments,
            DocumentCount: documentCount,
            RagStatus: ragStatus,
            BlockingReason: blockingReason);
    }
}
