namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for API requests grouped by day (for BarChart).
/// Issue #2790: Admin Dashboard Charts
/// </summary>
public record ApiRequestByDayDto(
    DateOnly Date,
    int Count
);
