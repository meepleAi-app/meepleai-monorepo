using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for UpdateAgentLlmConfigurationCommand.
/// Creates a new IsCurrent config row (versioning pattern) and deactivates the old one.
/// Enforces ownership + tier validation for model selection.
/// </summary>
internal sealed class UpdateAgentLlmConfigurationCommandHandler
    : IRequestHandler<UpdateAgentLlmConfigurationCommand, AgentConfigurationDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly MeepleAiDbContext _db;
    private readonly IModelConfigurationService _modelConfigService;
    private readonly ILogger<UpdateAgentLlmConfigurationCommandHandler> _logger;

    public UpdateAgentLlmConfigurationCommandHandler(
        IAgentRepository agentRepository,
        MeepleAiDbContext db,
        IModelConfigurationService modelConfigService,
        ILogger<UpdateAgentLlmConfigurationCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _modelConfigService = modelConfigService ?? throw new ArgumentNullException(nameof(modelConfigService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentConfigurationDto> Handle(
        UpdateAgentLlmConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load agent and verify ownership
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
            throw new NotFoundException("Agent", request.AgentId.ToString());

        var isAdmin = string.Equals(request.UserRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(request.UserRole, "Editor", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && agent.CreatedByUserId != request.UserId)
            throw new ForbiddenException("You can only update your own agents");

        // 2. Load current config
        var currentConfig = await _db.Set<AgentConfigurationEntity>()
            .FirstOrDefaultAsync(
                c => c.AgentId == request.AgentId && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        if (currentConfig is null)
            throw new NotFoundException("AgentConfiguration", request.AgentId.ToString());

        // 3. Resolve new values (partial update: keep old value when field is null)
        var newModel = request.ModelId ?? currentConfig.LlmModel;
        var newTemperature = request.Temperature ?? currentConfig.Temperature;
        var newMaxTokens = request.MaxTokens ?? currentConfig.MaxTokens;
        var newDocIdsJson = request.SelectedDocumentIds is not null
            ? JsonSerializer.Serialize(request.SelectedDocumentIds)
            : currentConfig.SelectedDocumentIdsJson;

        // 4. Tier validation for model change
        if (request.ModelId is not null)
        {
            if (!ModelTierExtensions.TryParse(request.UserTier, out var userTier))
                userTier = ModelTier.Free;

            // Admin bypasses tier restriction
            if (!isAdmin)
            {
                var validation = _modelConfigService.ValidateUserTierForModel(userTier, request.ModelId);
                if (!validation.IsValid)
                    throw new ForbiddenException(
                        $"Your tier ({validation.UserTier}) cannot access model '{request.ModelId}' (requires {validation.RequiredTier}). " +
                        "Upgrade your subscription to use this model.");
            }
        }

        // 5. Determine LlmProvider from model config
        var llmProvider = currentConfig.LlmProvider;
        var modelConfig = _modelConfigService.GetModelById(newModel);
        if (modelConfig is not null)
        {
            llmProvider = string.Equals(modelConfig.Provider, "ollama", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;
        }

        // 6. Create new config row and deactivate old (versioned IsCurrent pattern)
        var newConfig = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = request.AgentId,
            LlmProvider = llmProvider,
            LlmModel = newModel,
            AgentMode = currentConfig.AgentMode,
            SelectedDocumentIdsJson = newDocIdsJson,
            Temperature = newTemperature,
            MaxTokens = newMaxTokens,
            SystemPromptOverride = currentConfig.SystemPromptOverride,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.UserId
        };

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async ct =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            currentConfig.IsCurrent = false;
            _db.Set<AgentConfigurationEntity>().Update(currentConfig);
            _db.Set<AgentConfigurationEntity>().Add(newConfig);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} updated agent {AgentId} LLM config: model={Model}, temp={Temperature}, maxTokens={MaxTokens}",
            request.UserId, request.AgentId, newModel, newTemperature, newMaxTokens);

        return ToDto(newConfig);
    }

    internal static AgentConfigurationDto ToDto(AgentConfigurationEntity entity)
    {
        var docIds = !string.IsNullOrEmpty(entity.SelectedDocumentIdsJson)
            ? JsonSerializer.Deserialize<List<Guid>>(entity.SelectedDocumentIdsJson) ?? []
            : new List<Guid>();

        return new AgentConfigurationDto(
            Id: entity.Id,
            AgentId: entity.AgentId,
            LlmModel: entity.LlmModel,
            LlmProvider: entity.LlmProvider == 1 ? "Ollama" : "OpenRouter",
            Temperature: entity.Temperature,
            MaxTokens: entity.MaxTokens,
            SelectedDocumentIds: docIds,
            IsCurrent: entity.IsCurrent,
            CreatedAt: entity.CreatedAt);
    }
}
