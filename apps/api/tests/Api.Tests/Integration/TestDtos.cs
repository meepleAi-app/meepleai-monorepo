using System.Collections.Generic;

namespace Api.Tests.Integration;

/// <summary>
/// Shared DTOs for integration tests
/// </summary>
public record SystemConfigurationDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string ValueType { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public bool RequiresRestart { get; init; } = false;
    public string Environment { get; init; } = "All";
    public int Version { get; init; } = 1;
}

/// <summary>
/// Paged result for list endpoints
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize
);
