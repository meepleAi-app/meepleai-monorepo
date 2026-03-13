using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

/// <summary>
/// Command to update an existing tier definition by name.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal record UpdateTierDefinitionCommand(
    string Name,
    string? DisplayName,
    TierLimitsDto? Limits,
    string? LlmModelTier,
    bool? IsDefault
) : IRequest<TierDefinitionDto>;
