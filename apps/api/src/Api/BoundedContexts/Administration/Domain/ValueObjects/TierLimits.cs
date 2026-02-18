namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value Object representing tier resource limits (Issue #3692)
/// Extended with credit-based budget tracking (1 credit = $0.00001 USD)
/// </summary>
public sealed record TierLimits
{
    public int TokensPerMonth { get; init; }
    public int TokensPerDay { get; init; }
    public int MessagesPerDay { get; init; }
    public int MaxCollectionSize { get; init; }
    public int MaxPdfUploadsPerMonth { get; init; }
    public int MaxAgentsCreated { get; init; }

    // Credit-based budget limits (1 credit = $0.00001 USD)
    public decimal DailyCreditsLimit { get; init; }
    public decimal WeeklyCreditsLimit { get; init; }

    private TierLimits(
        int tokensPerMonth,
        int tokensPerDay,
        int messagesPerDay,
        int maxCollectionSize,
        int maxPdfUploadsPerMonth,
        int maxAgentsCreated,
        decimal dailyCreditsLimit,
        decimal weeklyCreditsLimit)
    {
        TokensPerMonth = tokensPerMonth;
        TokensPerDay = tokensPerDay;
        MessagesPerDay = messagesPerDay;
        MaxCollectionSize = maxCollectionSize;
        MaxPdfUploadsPerMonth = maxPdfUploadsPerMonth;
        MaxAgentsCreated = maxAgentsCreated;
        DailyCreditsLimit = dailyCreditsLimit;
        WeeklyCreditsLimit = weeklyCreditsLimit;
    }

    public static TierLimits Create(
        int tokensPerMonth,
        int tokensPerDay,
        int messagesPerDay,
        int maxCollectionSize,
        int maxPdfUploadsPerMonth,
        int maxAgentsCreated,
        decimal dailyCreditsLimit,
        decimal weeklyCreditsLimit)
    {
        if (tokensPerMonth < 0) throw new ArgumentException("Tokens per month cannot be negative", nameof(tokensPerMonth));
        if (tokensPerDay < 0) throw new ArgumentException("Tokens per day cannot be negative", nameof(tokensPerDay));
        if (messagesPerDay < 0) throw new ArgumentException("Messages per day cannot be negative", nameof(messagesPerDay));
        if (maxCollectionSize < 0) throw new ArgumentException("Max collection size cannot be negative", nameof(maxCollectionSize));
        if (maxPdfUploadsPerMonth < 0) throw new ArgumentException("Max PDF uploads cannot be negative", nameof(maxPdfUploadsPerMonth));
        if (maxAgentsCreated < 0) throw new ArgumentException("Max agents created cannot be negative", nameof(maxAgentsCreated));
        if (dailyCreditsLimit < 0) throw new ArgumentException("Daily credits limit cannot be negative", nameof(dailyCreditsLimit));
        if (weeklyCreditsLimit < 0) throw new ArgumentException("Weekly credits limit cannot be negative", nameof(weeklyCreditsLimit));

        return new TierLimits(
            tokensPerMonth,
            tokensPerDay,
            messagesPerDay,
            maxCollectionSize,
            maxPdfUploadsPerMonth,
            maxAgentsCreated,
            dailyCreditsLimit,
            weeklyCreditsLimit);
    }

    /// <summary>
    /// Default limits for Free tier
    /// Credits: 100/day, 10,000/week (= $0.001/day, $0.10/week)
    /// </summary>
    public static TierLimits FreeTier() => Create(
        tokensPerMonth: 10_000,
        tokensPerDay: 500,
        messagesPerDay: 10,
        maxCollectionSize: 20,
        maxPdfUploadsPerMonth: 5,
        maxAgentsCreated: 1,
        dailyCreditsLimit: 100m,
        weeklyCreditsLimit: 10_000m);

    /// <summary>
    /// Default limits for Basic tier
    /// Credits: 1,000/day, 5,000/week (= $0.01/day, $0.05/week)
    /// </summary>
    public static TierLimits BasicTier() => Create(
        tokensPerMonth: 50_000,
        tokensPerDay: 2_000,
        messagesPerDay: 50,
        maxCollectionSize: 50,
        maxPdfUploadsPerMonth: 20,
        maxAgentsCreated: 3,
        dailyCreditsLimit: 1_000m,
        weeklyCreditsLimit: 5_000m);

    /// <summary>
    /// Default limits for Pro tier
    /// Credits: 5,000/day, 25,000/week (= $0.05/day, $0.25/week)
    /// </summary>
    public static TierLimits ProTier() => Create(
        tokensPerMonth: 200_000,
        tokensPerDay: 10_000,
        messagesPerDay: 200,
        maxCollectionSize: 200,
        maxPdfUploadsPerMonth: 100,
        maxAgentsCreated: 10,
        dailyCreditsLimit: 5_000m,
        weeklyCreditsLimit: 25_000m);

    /// <summary>
    /// Default limits for Enterprise tier (unlimited = int.MaxValue / decimal.MaxValue)
    /// </summary>
    public static TierLimits EnterpriseTier() => Create(
        tokensPerMonth: int.MaxValue,
        tokensPerDay: int.MaxValue,
        messagesPerDay: int.MaxValue,
        maxCollectionSize: int.MaxValue,
        maxPdfUploadsPerMonth: int.MaxValue,
        maxAgentsCreated: int.MaxValue,
        dailyCreditsLimit: decimal.MaxValue,
        weeklyCreditsLimit: decimal.MaxValue);
}
