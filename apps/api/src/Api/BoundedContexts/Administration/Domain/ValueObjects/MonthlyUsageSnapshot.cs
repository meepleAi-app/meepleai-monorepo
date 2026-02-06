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
