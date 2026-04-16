namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

using Api.BoundedContexts.KnowledgeBase.Application.Models;

/// <summary>
/// Scans LLM response bodies for verbatim copyright leaks against Protected chunks.
/// Threshold, timeout, and failure mode configured via CopyrightLeakGuardOptions.
/// </summary>
internal interface ICopyrightLeakGuard
{
    /// <summary>
    /// Scans the response body for consecutive-word runs matching any Protected citation's
    /// FullText (or SnippetPreview as fallback). Returns HasLeak=true if any run of
    /// VerbatimRunThreshold+ consecutive words matches (case-insensitive, punctuation-normalized).
    /// </summary>
    /// <param name="responseBody">The full LLM response text to scan.</param>
    /// <param name="protectedCitations">Citations with CopyrightTier=Protected. Empty list is valid.</param>
    /// <param name="ct">Cancellation token. Timeout is enforced via caller-side CancelAfter.</param>
    Task<CopyrightLeakResult> ScanAsync(
        string responseBody,
        IReadOnlyList<ChunkCitation> protectedCitations,
        CancellationToken ct);
}

/// <summary>
/// Result of a copyright leak scan.
/// </summary>
internal sealed record CopyrightLeakResult(
    bool HasLeak,
    IReadOnlyList<LeakMatch> Matches);

/// <summary>
/// A single detected verbatim run match.
/// </summary>
internal sealed record LeakMatch(
    string DocumentId,
    int PageNumber,
    int RunLength,
    int BodyStartIndex,
    string MatchedText);
