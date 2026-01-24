namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for AI usage statistics by model (for DonutChart).
/// Issue #2790: Admin Dashboard Charts
/// </summary>
public record AiUsageStatsDto(
    string Model,
    long Tokens,
    double Percentage
);
