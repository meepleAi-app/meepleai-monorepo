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
