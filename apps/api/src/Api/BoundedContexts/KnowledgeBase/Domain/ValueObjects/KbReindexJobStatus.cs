namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Status of a KB reindex job. Issue #941 / ADR-057.
///
/// State machine:
///   Queued → Running → (Completed | Failed)
///
/// Persisted as string for forward-compatibility (new states added without
/// migration).
/// </summary>
public static class KbReindexJobStatus
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";

    public static bool IsTerminal(string status) =>
        string.Equals(status, Completed, StringComparison.Ordinal)
        || string.Equals(status, Failed, StringComparison.Ordinal);

    public static bool IsActive(string status) =>
        string.Equals(status, Queued, StringComparison.Ordinal)
        || string.Equals(status, Running, StringComparison.Ordinal);
}
