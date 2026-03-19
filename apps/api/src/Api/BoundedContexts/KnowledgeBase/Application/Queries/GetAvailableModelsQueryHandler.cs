using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetAvailableModelsQuery.
/// Retrieves available AI models with optional tier-based filtering.
/// Issue #3377: Models Tier Endpoint
/// </summary>
internal sealed class GetAvailableModelsQueryHandler
    : IRequestHandler<GetAvailableModelsQuery, GetModelsResponse>
{
    private readonly IModelConfigurationService _modelConfigurationService;
    private readonly ILogger<GetAvailableModelsQueryHandler> _logger;

    public GetAvailableModelsQueryHandler(
        IModelConfigurationService modelConfigurationService,
        ILogger<GetAvailableModelsQueryHandler> logger)
    {
        _modelConfigurationService = modelConfigurationService ?? throw new ArgumentNullException(nameof(modelConfigurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<GetModelsResponse> Handle(
        GetAvailableModelsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ModelConfiguration> models;

        if (!string.IsNullOrWhiteSpace(request.Tier))
        {
            if (!ModelTierExtensions.TryParse(request.Tier, out var tier))
            {
                _logger.LogWarning("Invalid tier filter: {Tier}", request.Tier);
                // Return empty list for invalid tier
                return Task.FromResult(new GetModelsResponse(Array.Empty<ModelDto>()));
            }

            models = _modelConfigurationService.GetModelsByTier(tier);
            _logger.LogInformation("Retrieved {Count} models for tier {Tier}", models.Count, tier);
        }
        else
        {
            models = _modelConfigurationService.GetAllModels();
            _logger.LogInformation("Retrieved all {Count} models", models.Count);
        }

        var dtos = models.Select(ToDto).ToList();
        return Task.FromResult(new GetModelsResponse(dtos));
    }

    private static ModelDto ToDto(ModelConfiguration model)
    {
        return new ModelDto(
            Id: model.Id,
            Name: model.Name,
            Provider: model.Provider,
            Tier: model.Tier.GetDisplayName().ToLowerInvariant(),
            CostPer1kInputTokens: model.CostPer1kInputTokens,
            CostPer1kOutputTokens: model.CostPer1kOutputTokens,
            MaxTokens: model.MaxTokens,
            SupportsStreaming: model.SupportsStreaming,
            Description: model.Description);
    }
}
