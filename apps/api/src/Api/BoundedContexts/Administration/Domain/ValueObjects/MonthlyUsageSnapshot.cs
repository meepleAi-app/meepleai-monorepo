namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value Object representing historical monthly usage (Issue #3692)
/// </summary>
public sealed record MonthlyUsageSnapshot
{
    public string Month { get; init; } // Format: "2026-01"
    public int TokensUsed { get; init; }
    public decimal Cost { get; init; }
    public int MessagesCount { get; init; }

    // Issue #3866: this record is persisted as a jsonb array on UserTokenUsage.History and read back
    // through a value converter. Without this attribute System.Text.Json refuses the type outright —
    // "Deserialization of types without a parameterless constructor … is not supported" — so any row
    // with a non-empty history could not be read at all. It never showed because a tracking test
    // context returned the in-memory instance and the converter never ran on the way back.
    [System.Text.Json.Serialization.JsonConstructor]
    private MonthlyUsageSnapshot(string month, int tokensUsed, decimal cost, int messagesCount)
    {
        Month = month;
        TokensUsed = tokensUsed;
        Cost = cost;
        MessagesCount = messagesCount;
    }

    public static MonthlyUsageSnapshot Create(string month, int tokensUsed, decimal cost, int messagesCount)
    {
        if (string.IsNullOrWhiteSpace(month)) throw new ArgumentException("Month cannot be empty", nameof(month));
        if (!System.Text.RegularExpressions.Regex.IsMatch(month, @"^\d{4}-\d{2}$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(100)))
            throw new ArgumentException("Month must be in format YYYY-MM", nameof(month));
        if (tokensUsed < 0) throw new ArgumentException("Tokens used cannot be negative", nameof(tokensUsed));
        if (cost < 0) throw new ArgumentException("Cost cannot be negative", nameof(cost));
        if (messagesCount < 0) throw new ArgumentException("Messages count cannot be negative", nameof(messagesCount));

        return new MonthlyUsageSnapshot(month, tokensUsed, cost, messagesCount);
    }

    public static MonthlyUsageSnapshot Empty(string month) => Create(month, 0, 0m, 0);
}
