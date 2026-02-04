namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO representing the complete tier-strategy access matrix.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
/// <param name="Tiers">Available user tiers.</param>
/// <param name="Strategies">Available RAG strategies.</param>
/// <param name="AccessMatrix">Access entries for each tier-strategy combination.</param>
public record TierStrategyMatrixDto(
    IReadOnlyList<string> Tiers,
    IReadOnlyList<StrategyInfoDto> Strategies,
    IReadOnlyList<TierStrategyAccessDto> AccessMatrix
);

/// <summary>
/// Information about a RAG strategy.
/// </summary>
/// <param name="Name">Strategy name (e.g., "FAST", "BALANCED").</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="Description">Strategy description.</param>
/// <param name="ComplexityLevel">Complexity level (0-5).</param>
/// <param name="RequiresAdmin">Whether strategy requires admin privileges.</param>
public record StrategyInfoDto(
    string Name,
    string DisplayName,
    string Description,
    int ComplexityLevel,
    bool RequiresAdmin
);

/// <summary>
/// DTO for a single tier-strategy access entry.
/// </summary>
/// <param name="Id">Unique identifier (null if using default).</param>
/// <param name="Tier">User tier name.</param>
/// <param name="Strategy">RAG strategy name.</param>
/// <param name="IsEnabled">Whether access is enabled.</param>
/// <param name="IsDefault">Whether this is from defaults (no database entry).</param>
public record TierStrategyAccessDto(
    Guid? Id,
    string Tier,
    string Strategy,
    bool IsEnabled,
    bool IsDefault
);
