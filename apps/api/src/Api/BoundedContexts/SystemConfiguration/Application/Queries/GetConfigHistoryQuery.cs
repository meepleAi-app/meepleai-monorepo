using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve configuration change history.
/// </summary>
public record GetConfigHistoryQuery(
    Guid ConfigurationId,
    int Limit = 20
) : IQuery<IReadOnlyList<ConfigurationHistoryDto>>;

/// <summary>
/// Configuration history entry with change tracking.
/// </summary>
public record ConfigurationHistoryDto(
    string Id,
    string ConfigurationId,
    string Key,
    string OldValue,
    string NewValue,
    int Version,
    DateTime ChangedAt,
    string ChangedByUserId,
    string ChangeReason
);
