namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value Object representing tier pricing information (Issue #3692)
/// </summary>
public sealed record TierPricing
{
    public decimal MonthlyFee { get; init; }
    public decimal? CostPerExtraToken { get; init; }
    public string Currency { get; init; }

    private TierPricing(decimal monthlyFee, decimal? costPerExtraToken, string currency)
    {
        MonthlyFee = monthlyFee;
        CostPerExtraToken = costPerExtraToken;
        Currency = currency;
    }

    public static TierPricing Create(decimal monthlyFee, decimal? costPerExtraToken = null, string currency = "EUR")
    {
        if (monthlyFee < 0) throw new ArgumentException("Monthly fee cannot be negative", nameof(monthlyFee));
        if (costPerExtraToken.HasValue && costPerExtraToken.Value < 0)
            throw new ArgumentException("Cost per extra token cannot be negative", nameof(costPerExtraToken));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency cannot be empty", nameof(currency));

        return new TierPricing(monthlyFee, costPerExtraToken, currency);
    }

    /// <summary>
    /// Free tier pricing (€0/month, no overage)
    /// </summary>
    public static TierPricing FreeTier() => Create(0m, null);

    /// <summary>
    /// Basic tier pricing (€9.99/month, €0.0001 per extra token)
    /// </summary>
    public static TierPricing BasicTier() => Create(9.99m, 0.0001m);

    /// <summary>
    /// Pro tier pricing (€29.99/month, €0.00008 per extra token)
    /// </summary>
    public static TierPricing ProTier() => Create(29.99m, 0.00008m);

    /// <summary>
    /// Enterprise tier pricing (custom, no overage)
    /// </summary>
    public static TierPricing EnterpriseTier() => Create(0m, null); // Custom negotiated
}
