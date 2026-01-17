namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Issue #2520: Model pricing information for cost tracking
/// Stored as JSONB in PostgreSQL
/// </summary>
public sealed record ModelPricing
{
    public decimal InputPricePerMillion { get; init; }
    public decimal OutputPricePerMillion { get; init; }
    public string Currency { get; init; }

    public ModelPricing(
        decimal inputPricePerMillion = 0m,
        decimal outputPricePerMillion = 0m,
        string currency = "USD")
    {
        if (inputPricePerMillion < 0)
            throw new ArgumentException("Input price cannot be negative", nameof(inputPricePerMillion));

        if (outputPricePerMillion < 0)
            throw new ArgumentException("Output price cannot be negative", nameof(outputPricePerMillion));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));

        InputPricePerMillion = inputPricePerMillion;
        OutputPricePerMillion = outputPricePerMillion;
        Currency = currency;
    }

    public static ModelPricing Free => new(0m, 0m);

    public decimal CalculateCost(int promptTokens, int completionTokens)
    {
        var inputCost = (promptTokens / 1_000_000m) * InputPricePerMillion;
        var outputCost = (completionTokens / 1_000_000m) * OutputPricePerMillion;
        return inputCost + outputCost;
    }
}
