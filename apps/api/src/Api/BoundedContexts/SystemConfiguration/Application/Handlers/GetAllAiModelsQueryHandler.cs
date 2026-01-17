using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public sealed class GetAllAiModelsQueryHandler : IQueryHandler<GetAllAiModelsQuery, IReadOnlyList<AiModelConfigDto>>
{
    private readonly IAiModelConfigurationRepository _repository;

    public GetAllAiModelsQueryHandler(IAiModelConfigurationRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<AiModelConfigDto>> Handle(GetAllAiModelsQuery request, CancellationToken cancellationToken)
    {
        var models = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return models.Select(MapToDto).ToList();
    }

    private static AiModelConfigDto MapToDto(Domain.Entities.AiModelConfiguration model) => new()
    {
        Id = model.Id,
        ModelId = model.ModelId,
        DisplayName = model.DisplayName,
        Provider = model.Provider,
        Priority = model.Priority,
        IsActive = model.IsActive,
        IsPrimary = model.IsPrimary,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt,
        Settings = new ModelSettingsDto
        {
            MaxTokens = model.Settings.MaxTokens,
            Temperature = model.Settings.Temperature,
            TopP = model.Settings.TopP,
            FrequencyPenalty = model.Settings.FrequencyPenalty,
            PresencePenalty = model.Settings.PresencePenalty
        },
        Pricing = new ModelPricingDto
        {
            InputPricePerMillion = model.Pricing.InputPricePerMillion,
            OutputPricePerMillion = model.Pricing.OutputPricePerMillion,
            Currency = model.Pricing.Currency
        },
        Usage = new UsageStatsDto
        {
            TotalRequests = model.Usage.TotalRequests,
            TotalTokensUsed = model.Usage.TotalTokensUsed,
            TotalCostUsd = model.Usage.TotalCostUsd,
            LastUsedAt = model.Usage.LastUsedAt
        }
    };
}
