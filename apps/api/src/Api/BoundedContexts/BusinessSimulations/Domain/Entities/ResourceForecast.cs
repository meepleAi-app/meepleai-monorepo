using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Saved resource forecasting scenario with 12-month projections.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
public sealed class ResourceForecast : AggregateRoot<Guid>
{
    /// <summary>User-defined name for this forecast scenario</summary>
    public string Name { get; private set; }

    /// <summary>Growth pattern used (Linear, Exponential, Logarithmic, SCurve)</summary>
    public string GrowthPattern { get; private set; }

    /// <summary>Monthly user growth rate as percentage (e.g. 10.0 = 10%)</summary>
    public decimal MonthlyGrowthRate { get; private set; }

    /// <summary>Current number of active users</summary>
    public int CurrentUsers { get; private set; }

    /// <summary>Current database size in GB</summary>
    public decimal CurrentDbSizeGb { get; private set; }

    /// <summary>Current daily token usage</summary>
    public long CurrentDailyTokens { get; private set; }

    /// <summary>Current cache memory in MB</summary>
    public decimal CurrentCacheMb { get; private set; }

    /// <summary>Current vector entry count</summary>
    public long CurrentVectorEntries { get; private set; }

    /// <summary>DB storage per user in MB</summary>
    public decimal DbPerUserMb { get; private set; }

    /// <summary>Tokens per user per day</summary>
    public int TokensPerUserPerDay { get; private set; }

    /// <summary>Cache per user in MB</summary>
    public decimal CachePerUserMb { get; private set; }

    /// <summary>Vector entries per user</summary>
    public int VectorsPerUser { get; private set; }

    /// <summary>JSON array of 12-month projection data points</summary>
    public string ProjectionsJson { get; private set; }

    /// <summary>JSON array of action recommendations</summary>
    public string? RecommendationsJson { get; private set; }

    /// <summary>Estimated total monthly cost at month 12 (USD)</summary>
    public decimal ProjectedMonthlyCost { get; private set; }

    /// <summary>User who created this forecast</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>When this forecast was created</summary>
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value
    private ResourceForecast() : base() { }
#pragma warning restore CS8618

    private ResourceForecast(
        Guid id,
        string name,
        string growthPattern,
        decimal monthlyGrowthRate,
        int currentUsers,
        decimal currentDbSizeGb,
        long currentDailyTokens,
        decimal currentCacheMb,
        long currentVectorEntries,
        decimal dbPerUserMb,
        int tokensPerUserPerDay,
        decimal cachePerUserMb,
        int vectorsPerUser,
        string projectionsJson,
        string? recommendationsJson,
        decimal projectedMonthlyCost,
        Guid createdByUserId) : base(id)
    {
        Name = name;
        GrowthPattern = growthPattern;
        MonthlyGrowthRate = monthlyGrowthRate;
        CurrentUsers = currentUsers;
        CurrentDbSizeGb = currentDbSizeGb;
        CurrentDailyTokens = currentDailyTokens;
        CurrentCacheMb = currentCacheMb;
        CurrentVectorEntries = currentVectorEntries;
        DbPerUserMb = dbPerUserMb;
        TokensPerUserPerDay = tokensPerUserPerDay;
        CachePerUserMb = cachePerUserMb;
        VectorsPerUser = vectorsPerUser;
        ProjectionsJson = projectionsJson;
        RecommendationsJson = recommendationsJson;
        ProjectedMonthlyCost = projectedMonthlyCost;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new saved resource forecast scenario.
    /// </summary>
    public static ResourceForecast Create(
        string name,
        string growthPattern,
        decimal monthlyGrowthRate,
        int currentUsers,
        decimal currentDbSizeGb,
        long currentDailyTokens,
        decimal currentCacheMb,
        long currentVectorEntries,
        decimal dbPerUserMb,
        int tokensPerUserPerDay,
        decimal cachePerUserMb,
        int vectorsPerUser,
        string projectionsJson,
        string? recommendationsJson,
        decimal projectedMonthlyCost,
        Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));

        if (string.IsNullOrWhiteSpace(growthPattern))
            throw new ArgumentException("GrowthPattern is required", nameof(growthPattern));

        if (monthlyGrowthRate < 0 || monthlyGrowthRate > 100)
            throw new ArgumentException("MonthlyGrowthRate must be between 0 and 100", nameof(monthlyGrowthRate));

        if (currentUsers < 0)
            throw new ArgumentException("CurrentUsers cannot be negative", nameof(currentUsers));

        if (currentDbSizeGb < 0)
            throw new ArgumentException("CurrentDbSizeGb cannot be negative", nameof(currentDbSizeGb));

        if (currentDailyTokens < 0)
            throw new ArgumentException("CurrentDailyTokens cannot be negative", nameof(currentDailyTokens));

        if (currentCacheMb < 0)
            throw new ArgumentException("CurrentCacheMb cannot be negative", nameof(currentCacheMb));

        if (currentVectorEntries < 0)
            throw new ArgumentException("CurrentVectorEntries cannot be negative", nameof(currentVectorEntries));

        if (string.IsNullOrWhiteSpace(projectionsJson))
            throw new ArgumentException("ProjectionsJson is required", nameof(projectionsJson));

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId cannot be empty", nameof(createdByUserId));

        return new ResourceForecast(
            Guid.NewGuid(),
            name.Trim(),
            growthPattern,
            monthlyGrowthRate,
            currentUsers,
            currentDbSizeGb,
            currentDailyTokens,
            currentCacheMb,
            currentVectorEntries,
            dbPerUserMb,
            tokensPerUserPerDay,
            cachePerUserMb,
            vectorsPerUser,
            projectionsJson,
            recommendationsJson,
            projectedMonthlyCost,
            createdByUserId);
    }
}
