namespace Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;

/// <summary>
/// Value object representing a monetary amount with currency.
/// Issue #3720: Financial Ledger Data Model
/// </summary>
public sealed record Money
{
    /// <summary>The monetary amount (always positive)</summary>
    public decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code (e.g., EUR, USD)</summary>
    public string Currency { get; init; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a new Money value object with validation.
    /// </summary>
    /// <param name="amount">The monetary amount (must be non-negative)</param>
    /// <param name="currency">ISO 4217 currency code (default: EUR)</param>
    public static Money Create(decimal amount, string currency = "EUR")
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code is required", nameof(currency));

        if (currency.Length is < 3 or > 3)
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code", nameof(currency));

        return new Money(amount, currency.ToUpperInvariant());
    }

    /// <summary>Creates a zero-amount Money in EUR</summary>
    public static Money Zero() => Create(0m);

    /// <summary>Creates a Money value in EUR</summary>
    public static Money InEur(decimal amount) => Create(amount, "EUR");
}
