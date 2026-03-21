using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Handler for CreateAgentDefinitionCommand.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class CreateAgentDefinitionCommandHandler
    : IRequestHandler<CreateAgentDefinitionCommand, AgentDefinitionDto>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ILogger<CreateAgentDefinitionCommandHandler> _logger;

    public CreateAgentDefinitionCommandHandler(
        IAgentDefinitionRepository repository,
        ILogger<CreateAgentDefinitionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDefinitionDto> Handle(
        CreateAgentDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate name uniqueness
        var exists = await _repository.ExistsAsync(request.Name, cancellationToken).ConfigureAwait(false);
        if (exists)
            throw new ConflictException($"AgentDefinition with name '{request.Name}' already exists");

        // Parse and validate type
        var type = AgentType.Parse(request.Type);

        // Create config value object
        var config = AgentDefinitionConfig.Create(request.Model, request.MaxTokens, request.Temperature);

        // Create strategy (default to HybridSearch if not provided)
        var strategy = !string.IsNullOrWhiteSpace(request.StrategyName)
            ? AgentStrategy.Custom(request.StrategyName, request.StrategyParameters ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
            : AgentStrategy.HybridSearch();

        // Create prompts
        var prompts = request.Prompts?
            .Select(p => AgentPromptTemplate.Create(p.Role, p.Content))
            .ToList();

        // Create tools
        var tools = request.Tools?
            .Select(t => AgentToolConfig.Create(t.Name, t.Settings))
            .ToList();

        // Create aggregate
        var agentDefinition = Domain.Entities.AgentDefinition.Create(
            request.Name,
            request.Description,
            type,
            config,
            strategy,
            prompts,
            tools);

        // Issue #5140: Apply KbCardIds if provided in the command
        if (request.KbCardIds is { Count: > 0 })
            agentDefinition.UpdateKbCardIds(request.KbCardIds);

        // Persist
        await _repository.AddAsync(agentDefinition, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created AgentDefinition {Id} with name '{Name}'",
            agentDefinition.Id,
            agentDefinition.Name);

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
            Status = agent.Status,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }
}
