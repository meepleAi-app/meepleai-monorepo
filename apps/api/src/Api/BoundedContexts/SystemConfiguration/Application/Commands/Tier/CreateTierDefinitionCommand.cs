using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

/// <summary>
/// Command to create a new tier definition.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal record CreateTierDefinitionCommand(
    string Name,
    string DisplayName,
    TierLimitsDto Limits,
    string LlmModelTier,
    bool IsDefault = false
) : IRequest<TierDefinitionDto>;
