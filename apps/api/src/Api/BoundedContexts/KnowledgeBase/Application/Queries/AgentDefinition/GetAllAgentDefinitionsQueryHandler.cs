using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;

/// <summary>
/// Handler for GetAllAgentDefinitionsQuery.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class GetAllAgentDefinitionsQueryHandler
    : IRequestHandler<GetAllAgentDefinitionsQuery, List<AgentDefinitionDto>>
{
    private readonly IAgentDefinitionRepository _repository;

    public GetAllAgentDefinitionsQueryHandler(IAgentDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<AgentDefinitionDto>> Handle(
        GetAllAgentDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agentDefinitions = request.ActiveOnly
            ? await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return agentDefinitions.Select(MapToDto).ToList();
    }

    private static AgentDefinitionDto MapToDto(Domain.Entities.AgentDefinition agent)
    {
        return new AgentDefinitionDto
        {
            Id = agent.Id,
            Name = agent.Name,
            Type = agent.Type.Value,
            StrategyName = agent.Strategy.Name,
            StrategyParameters = agent.Strategy.Parameters as Dictionary<string, object> ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Description = agent.Description,
            Config = new AgentConfigDto
            {
                Model = agent.Config.Model,
                MaxTokens = agent.Config.MaxTokens,
                Temperature = agent.Config.Temperature
            },
            Prompts = agent.Prompts.Select(p => new PromptTemplateDto
            {
                Role = p.Role,
                Content = p.Content
            }).ToList(),
            Tools = agent.Tools.Select(t => new ToolConfigDto
            {
                Name = t.Name,
                Settings = t.GetSettings() as Dictionary<string, object> ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            }).ToList(),
            Status = agent.Status,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }
}
