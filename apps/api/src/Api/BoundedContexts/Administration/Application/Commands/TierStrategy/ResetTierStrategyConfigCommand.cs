using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.TierStrategy;

/// <summary>
/// Command to reset tier-strategy configuration to defaults.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
/// <param name="ResetAccessMatrix">Whether to reset the tier-strategy access matrix.</param>
/// <param name="ResetModelMappings">Whether to reset the strategy-model mappings.</param>
internal record ResetTierStrategyConfigCommand(
    bool ResetAccessMatrix = true,
    bool ResetModelMappings = true
) : ICommand<TierStrategyResetResultDto>;

/// <summary>
/// Result of the reset operation.
/// </summary>
/// <param name="AccessEntriesDeleted">Number of access entries deleted.</param>
/// <param name="ModelMappingsDeleted">Number of model mappings deleted.</param>
/// <param name="Message">Status message.</param>
public record TierStrategyResetResultDto(
    int AccessEntriesDeleted,
    int ModelMappingsDeleted,
    string Message
);
