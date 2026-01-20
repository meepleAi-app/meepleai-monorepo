using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update tier routing configuration.
/// Issue #2596: LLM tier routing with test/production model separation.
/// </summary>
/// <param name="Tier">User tier to update routing for.</param>
/// <param name="ProductionModelId">Model ID for production environment.</param>
/// <param name="TestModelId">Model ID for test environment.</param>
internal sealed record UpdateTierRoutingCommand(
    LlmUserTier Tier,
    string ProductionModelId,
    string TestModelId
) : ICommand<TierRoutingDto>;
