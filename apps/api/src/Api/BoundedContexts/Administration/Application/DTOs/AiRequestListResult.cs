using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Result containing a paginated list of AI request logs.
/// </summary>
public record AiRequestListResult
{
    /// <summary>
    /// List of AI request log entities.
    /// </summary>
    public required List<AiRequestLogEntity> Requests { get; init; }

    /// <summary>
    /// Total count of requests matching the filter criteria (before pagination).
    /// </summary>
    public required int TotalCount { get; init; }
}
