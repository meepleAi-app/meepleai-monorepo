using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;

/// <summary>
/// Handler for UpdateAgentDefinitionCommand.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class UpdateAgentDefinitionCommandHandler
    : IRequestHandler<UpdateAgentDefinitionCommand, AgentDefinitionDto>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ILogger<UpdateAgentDefinitionCommandHandler> _logger;

    public UpdateAgentDefinitionCommandHandler(
        IAgentDefinitionRepository repository,
        ILogger<UpdateAgentDefinitionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDefinitionDto> Handle(
        UpdateAgentDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get existing
        var agentDefinition = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition == null)
            throw new NotFoundException($"AgentDefinition {request.Id} not found");

        // Parse and update type
        var type = AgentType.Parse(request.Type);
        agentDefinition.UpdateType(type);

        // Update strategy if provided
        if (!string.IsNullOrWhiteSpace(request.StrategyName))
        {
            var strategy = AgentStrategy.Custom(request.StrategyName, request.StrategyParameters ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
            agentDefinition.UpdateStrategy(strategy);
        }

        // Update config
        var config = AgentDefinitionConfig.Create(request.Model, request.MaxTokens, request.Temperature);
        agentDefinition.UpdateConfig(config);

        // Update name/description
        agentDefinition.UpdateNameAndDescription(request.Name, request.Description);

        // Update prompts
        if (request.Prompts != null)
        {
            var prompts = request.Prompts
                .Select(p => AgentPromptTemplate.Create(p.Role, p.Content))
                .ToList();
            agentDefinition.UpdatePrompts(prompts);
        }

        // Update tools
        if (request.Tools != null)
        {
            var tools = request.Tools
                .Select(t => AgentToolConfig.Create(t.Name, t.Settings))
                .ToList();
            agentDefinition.UpdateTools(tools);
        }

        // Issue #5140: Update KbCardIds if explicitly provided
        if (request.KbCardIds != null)
            agentDefinition.UpdateKbCardIds(request.KbCardIds);

        // Persist
        await _repository.UpdateAsync(agentDefinition, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated AgentDefinition {Id}",
            agentDefinition.Id);

        return MapToDto(agentDefinition);
    }

    private static AgentDefinitionDto MapToDto(Domain.Entities.AgentDefinition agent)
    {
        return new AgentDefinitionDto
        {
            Id = agent.Id,
            Name = agent.Name,
            Description = agent.Description,
            Type = agent.Type.Value,
            StrategyName = agent.Strategy.Name,
            StrategyParameters = agent.Strategy.Parameters as Dictionary<string, object> ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
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
            KbCardIds = agent.KbCardIds.ToList(),
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }
}
