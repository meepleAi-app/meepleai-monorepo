namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Value object for AI model configuration settings (Issue #2520)
/// Stored as JSONB in database for flexibility
/// </summary>
public sealed record ModelSettings
{
    public int MaxTokens { get; init; }
    public decimal Temperature { get; init; }
    public ModelPricing Pricing { get; init; }

    public ModelSettings(int maxTokens, decimal temperature, ModelPricing pricing)
    {
        if (maxTokens < 512 || maxTokens > 8192)
            throw new ArgumentException("MaxTokens must be between 512 and 8192", nameof(maxTokens));

        if (temperature < 0 || temperature > 2)
            throw new ArgumentException("Temperature must be between 0 and 2", nameof(temperature));

        MaxTokens = maxTokens;
        Temperature = temperature;
        Pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
    }

    public static ModelSettings Default => new(
        maxTokens: 4096,
        temperature: 0.7m,
        pricing: ModelPricing.Free
    );
}

/// <summary>
/// Value object for model pricing (Issue #2520)
/// Costs in USD per 1M tokens
/// </summary>
public sealed record ModelPricing
{
    public decimal InputCostPer1M { get; init; }
    public decimal OutputCostPer1M { get; init; }

    public ModelPricing(decimal inputCostPer1M, decimal outputCostPer1M)
    {
        if (inputCostPer1M < 0)
            throw new ArgumentException("InputCostPer1M cannot be negative", nameof(inputCostPer1M));

        if (outputCostPer1M < 0)
            throw new ArgumentException("OutputCostPer1M cannot be negative", nameof(outputCostPer1M));

        InputCostPer1M = inputCostPer1M;
        OutputCostPer1M = outputCostPer1M;
    }

    public static ModelPricing Free => new(0m, 0m);

    public decimal CalculateCost(int inputTokens, int outputTokens)
    {
        return (inputTokens * InputCostPer1M / 1_000_000m) +
               (outputTokens * OutputCostPer1M / 1_000_000m);
    }
}

/// <summary>
/// Value object for usage statistics (Issue #2520)
/// Stored as JSONB in database
/// </summary>
public sealed record UsageStats
{
    public long TotalRequests { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public decimal TotalCostUsd { get; init; }
    public DateTime? LastUsedAt { get; init; }

    public UsageStats(
        long totalRequests,
        long totalInputTokens,
        long totalOutputTokens,
        decimal totalCostUsd,
        DateTime? lastUsedAt)
    {
        TotalRequests = totalRequests;
        TotalInputTokens = totalInputTokens;
        TotalOutputTokens = totalOutputTokens;
        TotalCostUsd = totalCostUsd;
        LastUsedAt = lastUsedAt;
    }

    public static UsageStats Empty => new(0, 0, 0, 0m, null);

    public UsageStats TrackRequest(int inputTokens, int outputTokens, decimal cost)
    {
        return new UsageStats(
            TotalRequests + 1,
            TotalInputTokens + inputTokens,
            TotalOutputTokens + outputTokens,
            TotalCostUsd + cost,
            DateTime.UtcNow
        );
    }
}
