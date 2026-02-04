namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for strategy-to-model mapping configuration.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
/// <param name="Id">Unique identifier (null if using defaults).</param>
/// <param name="Strategy">RAG strategy name.</param>
/// <param name="Provider">LLM provider (e.g., "openrouter", "ollama").</param>
/// <param name="PrimaryModel">Primary LLM model ID.</param>
/// <param name="FallbackModels">Fallback model IDs if primary fails.</param>
/// <param name="IsCustomizable">Whether users can customize this mapping.</param>
/// <param name="AdminOnly">Whether this strategy is admin-only.</param>
/// <param name="IsDefault">Whether this is from defaults (no database entry).</param>
public record StrategyModelMappingDto(
    Guid? Id,
    string Strategy,
    string Provider,
    string PrimaryModel,
    IReadOnlyList<string> FallbackModels,
    bool IsCustomizable,
    bool AdminOnly,
    bool IsDefault
);
