using System.Text.RegularExpressions;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Extracts paraphrased snippets from LLM response text using [ref:documentId:pageNum] markers.
/// Used to map AI-generated paraphrases back to specific citations for Protected tier display.
/// </summary>
internal static partial class ParaphraseExtractor
{
    private const double SimilarityThreshold = 0.7;

    [GeneratedRegex(@"\[ref:(?:[^:]+):(?:\d+)\]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkerPattern();

    /// <summary>
    /// Extracts paraphrased text for a specific citation from the LLM response.
    /// Returns null if marker not found, text too similar to original, or marker was in user input (injection).
    /// </summary>
    public static string? Extract(
        string responseText,
        string documentId,
        int pageNumber,
        string originalSnippet,
        string? userInput = null)
    {
        var marker = $"[ref:{documentId}:{pageNumber}]";

        // Security: reject if marker appears in user input (prompt injection)
        if (userInput != null && userInput.Contains(marker, StringComparison.Ordinal))
            return null;

        var markerIndex = responseText.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return null;

        var textStart = markerIndex + marker.Length;
        var remaining = responseText[textStart..].TrimStart();

        // Extract until next marker or double newline
        var nextMarker = MarkerPattern().Match(remaining);
        var doubleNewline = remaining.IndexOf("\n\n", StringComparison.Ordinal);

        int endIndex;
        if (nextMarker.Success && (doubleNewline < 0 || nextMarker.Index < doubleNewline))
            endIndex = nextMarker.Index;
        else if (doubleNewline >= 0)
            endIndex = doubleNewline;
        else
            endIndex = remaining.Length;

        var extracted = remaining[..endIndex].Trim();

        if (string.IsNullOrWhiteSpace(extracted)) return null;

        // Similarity check: reject if too close to original (copyright leak)
        if (ComputeOverlap(originalSnippet, extracted) > SimilarityThreshold)
            return null;

        return extracted;
    }

    private static double ComputeOverlap(string original, string extracted)
    {
        var origWords = original.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var extWords = extracted.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

        if (origWords.Count == 0 || extWords.Count == 0) return 0;

        var intersection = origWords.Intersect(extWords, StringComparer.Ordinal).Count();
        return (double)intersection / Math.Max(origWords.Count, extWords.Count);
    }
}
