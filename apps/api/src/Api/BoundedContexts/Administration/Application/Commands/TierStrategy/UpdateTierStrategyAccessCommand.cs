using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.TierStrategy;

/// <summary>
/// Command to update tier-strategy access configuration.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
/// <param name="Tier">User tier name.</param>
/// <param name="Strategy">RAG strategy name.</param>
/// <param name="IsEnabled">Whether to enable or disable access.</param>
internal record UpdateTierStrategyAccessCommand(
    string Tier,
    string Strategy,
    bool IsEnabled
) : ICommand<TierStrategyAccessDto>;
