using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// DTO for AI model configuration
/// </summary>
/// <remarks>
/// Issue #2520, #2567: Complete AI model configuration with JSONB settings
/// </remarks>
public sealed record AiModelDto
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string DisplayName { get; init; }
    public required string Provider { get; init; }
    public required int Priority { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsPrimary { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Model settings (temperature, maxTokens, pricing)
    /// </summary>
    public required ModelSettings Settings { get; init; }

    /// <summary>
    /// Usage statistics (requests, tokens, cost)
    /// </summary>
    public required UsageStats Usage { get; init; }
}

/// <summary>
/// Paginated list response for AI models
/// </summary>
public sealed record AiModelListDto
{
    public required IReadOnlyList<AiModelDto> Models { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
