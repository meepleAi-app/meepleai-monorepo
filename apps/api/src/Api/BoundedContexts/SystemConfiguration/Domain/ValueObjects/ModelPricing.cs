namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Issue #2520: Model pricing information for cost tracking
/// Stored as JSONB in PostgreSQL
/// </summary>
/// <remarks>
/// #4164 (ADR-095): il catalogo modelli è l'unico listino. Oltre a ingresso e uscita porta il prezzo
/// della lettura dalla cache, la data in cui il prezzo è stato verificato e la fonte della verifica.
/// I tre campi sono facoltativi perché le righe scritte prima di #4164 non li hanno: un JSON senza
/// queste chiavi si legge con <c>null</c>.
/// </remarks>
public sealed record ModelPricing
{
    public decimal InputPricePerMillion { get; init; }
    public decimal OutputPricePerMillion { get; init; }
    public string Currency { get; init; }

    /// <summary>
    /// Prezzo per milione di token d'ingresso letti dalla cache del provider. <c>null</c> se il
    /// provider non ha un prezzo distinto per la cache o se non è noto.
    /// </summary>
    public decimal? CacheReadPricePerMillion { get; init; }

    /// <summary>
    /// Giorno in cui il prezzo è stato verificato alla fonte. Fra due verifiche dello stesso modello
    /// vince la più recente: è la regola con cui il seed aggiorna un catalogo già popolato.
    /// </summary>
    public DateOnly? VerifiedOn { get; init; }

    /// <summary>
    /// Dove è stato verificato il prezzo: l'URL del listino del provider, oppure <c>admin</c> quando
    /// lo ha inserito un amministratore.
    /// </summary>
    public string? Source { get; init; }

    public ModelPricing(
        decimal inputPricePerMillion = 0m,
        decimal outputPricePerMillion = 0m,
        string currency = "USD",
        decimal? cacheReadPricePerMillion = null,
        DateOnly? verifiedOn = null,
        string? source = null)
    {
        if (inputPricePerMillion < 0)
            throw new ArgumentException("Input price cannot be negative", nameof(inputPricePerMillion));

        if (outputPricePerMillion < 0)
            throw new ArgumentException("Output price cannot be negative", nameof(outputPricePerMillion));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));

        if (cacheReadPricePerMillion < 0)
            throw new ArgumentException("Cache read price cannot be negative", nameof(cacheReadPricePerMillion));

        InputPricePerMillion = inputPricePerMillion;
        OutputPricePerMillion = outputPricePerMillion;
        Currency = currency;
        CacheReadPricePerMillion = cacheReadPricePerMillion;
        VerifiedOn = verifiedOn;
        Source = source;
    }

    public static ModelPricing Free => new(0m, 0m);

    public decimal CalculateCost(int promptTokens, int completionTokens)
    {
        var inputCost = (promptTokens / 1_000_000m) * InputPricePerMillion;
        var outputCost = (completionTokens / 1_000_000m) * OutputPricePerMillion;
        return inputCost + outputCost;
    }
}
