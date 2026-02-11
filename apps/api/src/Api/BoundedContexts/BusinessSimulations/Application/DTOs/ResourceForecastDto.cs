using Api.BoundedContexts.BusinessSimulations.Domain.Entities;

namespace Api.BoundedContexts.BusinessSimulations.Application.DTOs;

/// <summary>
/// DTO for a saved resource forecast scenario.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed record ResourceForecastDto(
    Guid Id,
    string Name,
    string GrowthPattern,
    decimal MonthlyGrowthRate,
    int CurrentUsers,
    decimal CurrentDbSizeGb,
    long CurrentDailyTokens,
    decimal CurrentCacheMb,
    long CurrentVectorEntries,
    decimal DbPerUserMb,
    int TokensPerUserPerDay,
    decimal CachePerUserMb,
    int VectorsPerUser,
    decimal ProjectedMonthlyCost,
    Guid CreatedByUserId,
    DateTime CreatedAt)
{
    public static ResourceForecastDto FromEntity(ResourceForecast entity)
    {
        return new ResourceForecastDto(
            Id: entity.Id,
            Name: entity.Name,
            GrowthPattern: entity.GrowthPattern,
            MonthlyGrowthRate: entity.MonthlyGrowthRate,
            CurrentUsers: entity.CurrentUsers,
            CurrentDbSizeGb: entity.CurrentDbSizeGb,
            CurrentDailyTokens: entity.CurrentDailyTokens,
            CurrentCacheMb: entity.CurrentCacheMb,
            CurrentVectorEntries: entity.CurrentVectorEntries,
            DbPerUserMb: entity.DbPerUserMb,
            TokensPerUserPerDay: entity.TokensPerUserPerDay,
            CachePerUserMb: entity.CachePerUserMb,
            VectorsPerUser: entity.VectorsPerUser,
            ProjectedMonthlyCost: entity.ProjectedMonthlyCost,
            CreatedByUserId: entity.CreatedByUserId,
            CreatedAt: entity.CreatedAt);
    }
}

/// <summary>
/// Paginated response DTO for resource forecasts.
/// </summary>
internal sealed record ResourceForecastsResponseDto(
    List<ResourceForecastDto> Items,
    int Total,
    int Page,
    int PageSize);
