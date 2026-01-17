using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public sealed record UpdateAiModelConfigCommand(
    Guid Id,
    string? DisplayName,
    int? Priority,
    bool? IsActive,
    bool? IsPrimary,
    ModelSettingsDto? Settings,
    ModelPricingDto? Pricing
) : ICommand<AiModelConfigDto>;
