using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetLinkedAgentQuery.
/// Fetches the linked AgentDefinition for a shared game.
/// Returns null if game has no linked agent (returns NoContent from endpoint).
/// Issue #4924
/// </summary>
internal sealed class GetLinkedAgentQueryHandler
    : IRequestHandler<GetLinkedAgentQuery, AgentDefinitionDto?>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly IAgentDefinitionRepository _agentRepository;
    private readonly ILogger<GetLinkedAgentQueryHandler> _logger;

    public GetLinkedAgentQueryHandler(
        ISharedGameRepository gameRepository,
        IAgentDefinitionRepository agentRepository,
        ILogger<GetLinkedAgentQueryHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDefinitionDto?> Handle(
        GetLinkedAgentQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var game = await _gameRepository.GetByIdAsync(request.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            _logger.LogWarning("GetLinkedAgent: shared game {SharedGameId} not found", request.SharedGameId);
            return null;
        }

        if (game.AgentDefinitionId is null)
            return null;

        var agentDef = await _agentRepository.GetByIdAsync(game.AgentDefinitionId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (agentDef is null)
        {
            _logger.LogWarning("GetLinkedAgent: agent definition {AgentId} not found for game {SharedGameId}",
                game.AgentDefinitionId, request.SharedGameId);
            return null;
        }

        return new AgentDefinitionDto
        {
            Id = agentDef.Id,
            Name = agentDef.Name,
            Description = agentDef.Description,
            Type = agentDef.Type.Value,
            Config = new AgentConfigDto
            {
                Model = agentDef.Config.Model,
                MaxTokens = agentDef.Config.MaxTokens,
                Temperature = agentDef.Config.Temperature
            },
            StrategyName = agentDef.Strategy.Name,
            StrategyParameters = agentDef.Strategy.Parameters as Dictionary<string, object>
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Prompts = agentDef.Prompts.Select(p => new PromptTemplateDto
            {
                Role = p.Role,
                Content = p.Content
            }).ToList(),
            Tools = agentDef.Tools.Select(t => new ToolConfigDto
            {
                Name = t.Name,
                Settings = t.GetSettings() as Dictionary<string, object>
                    ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            }).ToList(),
            KbCardIds = agentDef.KbCardIds.ToList(),
            Status = agentDef.Status,
            IsActive = agentDef.IsActive,
            CreatedAt = agentDef.CreatedAt,
            UpdatedAt = agentDef.UpdatedAt
        };
    }
}
