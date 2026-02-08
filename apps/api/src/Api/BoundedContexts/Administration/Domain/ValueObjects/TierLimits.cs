namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value Object representing tier resource limits (Issue #3692)
/// </summary>
public sealed record TierLimits
{
    public int TokensPerMonth { get; init; }
    public int TokensPerDay { get; init; }
    public int MessagesPerDay { get; init; }
    public int MaxCollectionSize { get; init; }
    public int MaxPdfUploadsPerMonth { get; init; }
    public int MaxAgentsCreated { get; init; }

    private TierLimits(
        int tokensPerMonth,
        int tokensPerDay,
        int messagesPerDay,
        int maxCollectionSize,
        int maxPdfUploadsPerMonth,
        int maxAgentsCreated)
    {
        TokensPerMonth = tokensPerMonth;
        TokensPerDay = tokensPerDay;
        MessagesPerDay = messagesPerDay;
        MaxCollectionSize = maxCollectionSize;
        MaxPdfUploadsPerMonth = maxPdfUploadsPerMonth;
        MaxAgentsCreated = maxAgentsCreated;
    }

    public static TierLimits Create(
        int tokensPerMonth,
        int tokensPerDay,
        int messagesPerDay,
        int maxCollectionSize,
        int maxPdfUploadsPerMonth,
        int maxAgentsCreated)
    {
        if (tokensPerMonth < 0) throw new ArgumentException("Tokens per month cannot be negative", nameof(tokensPerMonth));
        if (tokensPerDay < 0) throw new ArgumentException("Tokens per day cannot be negative", nameof(tokensPerDay));
        if (messagesPerDay < 0) throw new ArgumentException("Messages per day cannot be negative", nameof(messagesPerDay));
        if (maxCollectionSize < 0) throw new ArgumentException("Max collection size cannot be negative", nameof(maxCollectionSize));
        if (maxPdfUploadsPerMonth < 0) throw new ArgumentException("Max PDF uploads cannot be negative", nameof(maxPdfUploadsPerMonth));
        if (maxAgentsCreated < 0) throw new ArgumentException("Max agents created cannot be negative", nameof(maxAgentsCreated));

        return new TierLimits(
            tokensPerMonth,
            tokensPerDay,
            messagesPerDay,
            maxCollectionSize,
            maxPdfUploadsPerMonth,
            maxAgentsCreated);
    }

    /// <summary>
    /// Default limits for Free tier
    /// </summary>
    public static TierLimits FreeTier() => Create(
        tokensPerMonth: 10_000,
        tokensPerDay: 500,
        messagesPerDay: 10,
        maxCollectionSize: 20,
        maxPdfUploadsPerMonth: 5,
        maxAgentsCreated: 1);

    /// <summary>
    /// Default limits for Basic tier
    /// </summary>
    public static TierLimits BasicTier() => Create(
        tokensPerMonth: 50_000,
        tokensPerDay: 2_000,
        messagesPerDay: 50,
        maxCollectionSize: 50,
        maxPdfUploadsPerMonth: 20,
        maxAgentsCreated: 3);

    /// <summary>
    /// Default limits for Pro tier
    /// </summary>
    public static TierLimits ProTier() => Create(
        tokensPerMonth: 200_000,
        tokensPerDay: 10_000,
        messagesPerDay: 200,
        maxCollectionSize: 200,
        maxPdfUploadsPerMonth: 100,
        maxAgentsCreated: 10);

    /// <summary>
    /// Default limits for Enterprise tier (unlimited = int.MaxValue)
    /// </summary>
    public static TierLimits EnterpriseTier() => Create(
        tokensPerMonth: int.MaxValue,
        tokensPerDay: int.MaxValue,
        messagesPerDay: int.MaxValue,
        maxCollectionSize: int.MaxValue,
        maxPdfUploadsPerMonth: int.MaxValue,
        maxAgentsCreated: int.MaxValue);
}
