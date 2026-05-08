namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

public sealed record GamebookProgress
{
    public int CurrentParagraph { get; }
    public IReadOnlyList<int> History { get; }
    public DateTimeOffset LastReadAt { get; }

    private GamebookProgress(int current, IReadOnlyList<int> history, DateTimeOffset lastReadAt)
    {
        CurrentParagraph = current;
        History = history;
        LastReadAt = lastReadAt;
    }

    public static GamebookProgress Create(int currentParagraph, IEnumerable<int> history)
    {
        if (currentParagraph < 0)
            throw new ArgumentException("paragraph must be >= 0", nameof(currentParagraph));

        var historyList = history?.ToList() ?? new List<int>();
        if (currentParagraph > 0 && (historyList.Count == 0 || historyList[^1] != currentParagraph))
            historyList.Add(currentParagraph);

        return new GamebookProgress(currentParagraph, historyList.AsReadOnly(), DateTimeOffset.UtcNow);
    }

    public static GamebookProgress Empty() => new(0, Array.Empty<int>(), DateTimeOffset.UtcNow);
}
