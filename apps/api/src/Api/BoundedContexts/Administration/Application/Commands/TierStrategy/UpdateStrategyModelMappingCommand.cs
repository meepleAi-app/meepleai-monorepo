using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.TierStrategy;

/// <summary>
/// Command to update strategy-to-model mapping configuration.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
/// <param name="Strategy">RAG strategy name.</param>
/// <param name="Provider">LLM provider name.</param>
/// <param name="PrimaryModel">Primary model ID.</param>
/// <param name="FallbackModels">Fallback model IDs (optional).</param>
internal record UpdateStrategyModelMappingCommand(
    string Strategy,
    string Provider,
    string PrimaryModel,
    IReadOnlyList<string>? FallbackModels = null
) : ICommand<StrategyModelMappingDto>;
