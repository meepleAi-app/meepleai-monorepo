using Api.BoundedContexts.SystemConfiguration.Domain.Enums;

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// DTO for tier routing configuration.
/// Issue #2596: LLM tier routing with test/production model separation.
/// </summary>
public sealed record TierRoutingDto
{
    /// <summary>User tier this configuration applies to.</summary>
    public required LlmUserTier Tier { get; init; }

    /// <summary>Human-readable tier name.</summary>
    public required string TierName { get; init; }

    /// <summary>Production model ID.</summary>
    public string? ProductionModelId { get; init; }

    /// <summary>Production model display name.</summary>
    public string? ProductionModelName { get; init; }

    /// <summary>Production model provider (OpenRouter, Ollama).</summary>
    public string? ProductionProvider { get; init; }

    /// <summary>Test model ID.</summary>
    public string? TestModelId { get; init; }

    /// <summary>Test model display name.</summary>
    public string? TestModelName { get; init; }

    /// <summary>Test model provider (OpenRouter, Ollama).</summary>
    public string? TestProvider { get; init; }

    /// <summary>Estimated monthly cost in USD based on usage stats.</summary>
    public decimal EstimatedMonthlyCostUsd { get; init; }
}

/// <summary>
/// List response for tier routing configurations.
/// </summary>
public sealed record TierRoutingListDto
{
    /// <summary>All tier routing configurations.</summary>
    public required IReadOnlyList<TierRoutingDto> Routings { get; init; }

    /// <summary>Total number of configured tiers.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Current environment (Production/Development/Test).</summary>
    public required string CurrentEnvironment { get; init; }
}
