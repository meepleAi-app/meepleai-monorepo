

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

internal record ConfigurationDto(
    Guid Id,
    string Key,
    string Value,
    string ValueType,
    string? Description,
    string Category,
    bool IsActive,
    bool RequiresRestart,
    string Environment,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

internal record FeatureFlagDto(
    Guid Id,
    string Name,
    bool IsEnabled,
    string? Description,
    DateTime UpdatedAt
);
