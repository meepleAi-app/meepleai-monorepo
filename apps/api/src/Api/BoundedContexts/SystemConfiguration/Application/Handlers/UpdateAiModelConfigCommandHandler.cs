using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public sealed class UpdateAiModelConfigCommandHandler : ICommandHandler<UpdateAiModelConfigCommand, AiModelConfigDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly MeepleAiDbContext _db;

    public UpdateAiModelConfigCommandHandler(
        IAiModelConfigurationRepository repository,
        MeepleAiDbContext db)
    {
        _repository = repository;
        _db = db;
    }

    public async Task<AiModelConfigDto> Handle(UpdateAiModelConfigCommand request, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"AI model {request.Id} not found");

        if (request.Priority.HasValue)
            model.UpdatePriority(request.Priority.Value);

        if (request.IsActive.HasValue)
            model.SetActive(request.IsActive.Value);

        if (request.IsPrimary.HasValue)
            model.SetPrimary(request.IsPrimary.Value);

        if (request.Settings != null)
        {
            var settings = new ModelSettings(
                request.Settings.MaxTokens,
                request.Settings.Temperature,
                request.Settings.TopP,
                request.Settings.FrequencyPenalty,
                request.Settings.PresencePenalty);
            model.UpdateSettings(settings);
        }

        if (request.Pricing != null)
        {
            var pricing = new ModelPricing(
                request.Pricing.InputPricePerMillion,
                request.Pricing.OutputPricePerMillion,
                request.Pricing.Currency);
            model.UpdatePricing(pricing);
        }

        await _repository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(model);
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
