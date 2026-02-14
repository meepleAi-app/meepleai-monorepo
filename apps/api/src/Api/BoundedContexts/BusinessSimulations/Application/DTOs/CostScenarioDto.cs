using System.Text.Json;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;

namespace Api.BoundedContexts.BusinessSimulations.Application.DTOs;

/// <summary>
/// DTO for a saved cost scenario.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed record CostScenarioDto(
    Guid Id,
    string Name,
    string Strategy,
    string ModelId,
    int MessagesPerDay,
    int ActiveUsers,
    int AvgTokensPerRequest,
    decimal CostPerRequest,
    decimal DailyProjection,
    decimal MonthlyProjection,
    List<string> Warnings,
    Guid CreatedByUserId,
    DateTime CreatedAt)
{
    public static CostScenarioDto FromEntity(CostScenario entity)
    {
        List<string> warnings;
        try
        {
            warnings = string.IsNullOrEmpty(entity.Warnings)
                ? []
                : JsonSerializer.Deserialize<List<string>>(entity.Warnings) ?? [];
        }
        catch (JsonException)
        {
            warnings = [];
        }

        return new CostScenarioDto(
            Id: entity.Id,
            Name: entity.Name,
            Strategy: entity.Strategy,
            ModelId: entity.ModelId,
            MessagesPerDay: entity.MessagesPerDay,
            ActiveUsers: entity.ActiveUsers,
            AvgTokensPerRequest: entity.AvgTokensPerRequest,
            CostPerRequest: entity.CostPerRequest,
            DailyProjection: entity.DailyProjection,
            MonthlyProjection: entity.MonthlyProjection,
            Warnings: warnings,
            CreatedByUserId: entity.CreatedByUserId,
            CreatedAt: entity.CreatedAt);
    }
}

/// <summary>
/// Paginated response DTO for cost scenarios.
/// </summary>
internal sealed record CostScenariosResponseDto(
    List<CostScenarioDto> Items,
    int Total,
    int Page,
    int PageSize);
