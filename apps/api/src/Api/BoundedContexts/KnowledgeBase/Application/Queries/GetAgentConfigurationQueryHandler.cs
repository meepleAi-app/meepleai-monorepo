using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetAgentConfigurationQuery"/>. Returns the current LLM
/// configuration view for an <see cref="AgentDefinition"/>. Issue #657 (Phase δ).
/// </summary>
/// <remarks>
/// MVP scope decisions (see PR #696 description):
/// <list type="bullet">
///   <item><c>Id</c> mirrors <c>AgentId</c> (no separate AgentConfiguration aggregate exposed).</item>
///   <item><c>LlmProvider</c> is heuristically derived from the model name prefix.</item>
///   <item><c>SelectedDocumentIds</c> is empty (KB linking deferred — same as PR #695).</item>
///   <item><c>IsCurrent</c> is always <c>true</c> (single config per agent in MVP).</item>
/// </list>
/// Returns <c>null</c> when no agent matches the supplied id (route surfaces 404).
/// </remarks>
internal sealed class GetAgentConfigurationQueryHandler
    : IRequestHandler<GetAgentConfigurationQuery, AgentConfigurationDto?>
{
    private readonly IAgentDefinitionRepository _repository;

    public GetAgentConfigurationQueryHandler(IAgentDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AgentConfigurationDto?> Handle(
        GetAgentConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await _repository
            .GetByIdAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        return agent is null ? null : BuildViewDto(agent);
    }

    /// <summary>
    /// Maps an <see cref="AgentDefinitionEntity"/> aggregate to <see cref="AgentConfigurationDto"/>.
    /// Shared with <c>UpdateAgentConfigurationCommandHandler</c> so the PATCH route returns
    /// the same view shape as GET. Internal so the sibling handler can call it directly.
    /// </summary>
    internal static AgentConfigurationDto BuildViewDto(AgentDefinitionEntity agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return new AgentConfigurationDto(
            Id: agent.Id,
            AgentId: agent.Id,
            LlmModel: agent.Config.Model,
            LlmProvider: InferProvider(agent.Config.Model),
            Temperature: (decimal)agent.Config.Temperature,
            MaxTokens: agent.Config.MaxTokens,
            SelectedDocumentIds: Array.Empty<Guid>(),
            IsCurrent: true,
            CreatedAt: agent.CreatedAt);
    }

    /// <summary>
    /// Heuristic provider lookup keyed off the model identifier prefix.
    /// Falls back to "openai" when no rule matches (most common production model).
    /// </summary>
    private static string InferProvider(string model)
    {
        if (string.IsNullOrEmpty(model))
            return "openai";

        if (model.StartsWith("gpt", StringComparison.OrdinalIgnoreCase))
            return "openai";

        if (model.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            return "anthropic";

        if (model.Contains("llama", StringComparison.OrdinalIgnoreCase))
            return "ollama";

        if (model.Contains("mistral", StringComparison.OrdinalIgnoreCase))
            return "mistral";

        return "openai";
    }
}
