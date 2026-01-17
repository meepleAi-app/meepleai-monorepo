using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public sealed record CreateAiModelConfigCommand(
    string ModelId,
    string DisplayName,
    string Provider,
    int Priority,
    ModelSettingsDto? Settings,
    ModelPricingDto? Pricing
) : ICommand<AiModelConfigDto>;
