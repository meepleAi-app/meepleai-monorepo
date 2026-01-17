namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Issue #2520: Model generation settings (temperature, max tokens, etc.)
/// Stored as JSONB in PostgreSQL for schema flexibility
/// </summary>
public sealed record ModelSettings
{
    public int MaxTokens { get; init; }
    public double Temperature { get; init; }
    public double? TopP { get; init; }
    public double? FrequencyPenalty { get; init; }
    public double? PresencePenalty { get; init; }

    public ModelSettings(
        int maxTokens = 4096,
        double temperature = 0.7,
        double? topP = null,
        double? frequencyPenalty = null,
        double? presencePenalty = null)
    {
        if (maxTokens <= 0)
            throw new ArgumentException("MaxTokens must be > 0", nameof(maxTokens));

        if (temperature < 0 || temperature > 2)
            throw new ArgumentException("Temperature must be between 0 and 2", nameof(temperature));

        MaxTokens = maxTokens;
        Temperature = temperature;
        TopP = topP;
        FrequencyPenalty = frequencyPenalty;
        PresencePenalty = presencePenalty;
    }

    public static ModelSettings Default => new();
}
