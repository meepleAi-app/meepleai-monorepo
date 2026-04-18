using Api.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Matches LLM response text against RAG snippets to find inline citation positions.
/// Supports exact, fuzzy (Levenshtein), and n-gram overlap matching strategies.
/// </summary>
internal sealed class InlineCitationMatcherService
{
    private const double ExactMatchConfidence = 1.0;
    private const double FuzzyMatchConfidence = 0.8;
    private const double NgramMatchConfidence = 0.65;
    private const double MaxLevenshteinDistanceRatio = 0.15;
    private const double MinNgramOverlap = 0.6;
    private const int NgramSize = 3;
    private const int MinPhraseWords = 5;

    private static readonly char[] SentenceDelimiters = { '.', '!', '?' };
    private static readonly char[] WordDelimiters = { ' ', '\t' };

    /// <summary>
    /// Matches significant phrases from snippets against the answer text.
    /// Returns non-overlapping matches ordered by position.
    /// </summary>
    public IReadOnlyList<InlineCitationMatch> Match(string answer, IReadOnlyList<Snippet> snippets)
    {
        if (string.IsNullOrWhiteSpace(answer) || snippets == null || snippets.Count == 0)
            return Array.Empty<InlineCitationMatch>();

        var allMatches = new List<InlineCitationMatch>();

        for (int snippetIndex = 0; snippetIndex < snippets.Count; snippetIndex++)
        {
            var snippet = snippets[snippetIndex];
            if (string.IsNullOrWhiteSpace(snippet.text))
                continue;

            var pdfDocumentId = ExtractPdfDocumentId(snippet.source);
            var phrases = ExtractSignificantPhrases(snippet.text);

            // Try matching each phrase
            foreach (var phrase in phrases)
            {
                var match = TryMatch(answer, phrase, snippetIndex, snippet.page, pdfDocumentId);
                if (match != null)
                    allMatches.Add(match);
            }

            // Also try matching full snippet text
            var fullMatch = TryMatch(answer, snippet.text, snippetIndex, snippet.page, pdfDocumentId);
            if (fullMatch != null)
                allMatches.Add(fullMatch);
        }

        return ResolveOverlaps(allMatches);
    }

    private InlineCitationMatch? TryMatch(string answer, string phrase, int snippetIndex, int pageNumber, string pdfDocumentId)
    {
        // Strategy 1: Exact substring match
        var exactIndex = answer.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (exactIndex >= 0)
        {
            return new InlineCitationMatch(
                exactIndex, exactIndex + phrase.Length, snippetIndex, pageNumber, pdfDocumentId, ExactMatchConfidence);
        }

        // Strategy 2: Fuzzy match via Levenshtein distance
        var fuzzyMatch = TryFuzzyMatch(answer, phrase, snippetIndex, pageNumber, pdfDocumentId);
        if (fuzzyMatch != null)
            return fuzzyMatch;

        // Strategy 3: N-gram overlap
        var ngramMatch = TryNgramMatch(answer, phrase, snippetIndex, pageNumber, pdfDocumentId);
        if (ngramMatch != null)
            return ngramMatch;

        return null;
    }

    private static InlineCitationMatch? TryFuzzyMatch(string answer, string phrase, int snippetIndex, int pageNumber, string pdfDocumentId)
    {
        if (phrase.Length < 10) // Too short for meaningful fuzzy matching
            return null;

        var maxDistance = (int)(phrase.Length * MaxLevenshteinDistanceRatio);
        var windowSize = phrase.Length;

        // Slide window across answer
        for (int i = 0; i <= answer.Length - windowSize; i++)
        {
            var window = answer.Substring(i, windowSize);
            var distance = LevenshteinDistance(window.ToLowerInvariant(), phrase.ToLowerInvariant());

            if (distance <= maxDistance)
            {
                return new InlineCitationMatch(
                    i, i + windowSize, snippetIndex, pageNumber, pdfDocumentId, FuzzyMatchConfidence);
            }
        }

        return null;
    }

    private static InlineCitationMatch? TryNgramMatch(string answer, string phrase, int snippetIndex, int pageNumber, string pdfDocumentId)
    {
        if (phrase.Length < NgramSize * 2) // Too short for n-gram analysis
            return null;

        var phraseNgrams = GetNgrams(phrase.ToLowerInvariant(), NgramSize);
        if (phraseNgrams.Count == 0)
            return null;

        var windowSize = phrase.Length;
        var bestOverlap = 0.0;
        var bestStart = -1;

        // Slide window across answer with step size for efficiency
        var step = Math.Max(1, windowSize / 4);
        for (int i = 0; i <= answer.Length - windowSize; i += step)
        {
            var window = answer.Substring(i, Math.Min(windowSize, answer.Length - i));
            var windowNgrams = GetNgrams(window.ToLowerInvariant(), NgramSize);

            if (windowNgrams.Count == 0)
                continue;

            var intersection = phraseNgrams.Intersect(windowNgrams, StringComparer.Ordinal).Count();
            var overlap = (double)intersection / phraseNgrams.Count;

            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestStart = i;
            }
        }

        if (bestOverlap > MinNgramOverlap && bestStart >= 0)
        {
            return new InlineCitationMatch(
                bestStart,
                bestStart + Math.Min(windowSize, answer.Length - bestStart),
                snippetIndex, pageNumber, pdfDocumentId, NgramMatchConfidence);
        }

        return null;
    }

    internal static List<string> ExtractSignificantPhrases(string text)
    {
        var phrases = new List<string>();
        var sentences = text.Split(SentenceDelimiters, StringSplitOptions.RemoveEmptyEntries);

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            var wordCount = trimmed.Split(WordDelimiters, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > MinPhraseWords)
            {
                phrases.Add(trimmed);
            }
        }

        return phrases;
    }

    internal static string ExtractPdfDocumentId(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        // Expected format: "PDF:uuid"
        if (source.StartsWith("PDF:", StringComparison.OrdinalIgnoreCase) && source.Length > 4)
            return source.Substring(4);

        return source;
    }

    private static IReadOnlyList<InlineCitationMatch> ResolveOverlaps(List<InlineCitationMatch> matches)
    {
        if (matches.Count <= 1)
            return matches;

        // Sort by confidence descending, then by length descending
        var sorted = matches
            .OrderByDescending(m => m.Confidence)
            .ThenByDescending(m => m.EndOffset - m.StartOffset)
            .ToList();

        var resolved = new List<InlineCitationMatch>();
        foreach (var candidate in sorted)
        {
            var overlaps = resolved.Any(existing =>
                candidate.StartOffset < existing.EndOffset && candidate.EndOffset > existing.StartOffset);

            if (!overlaps)
                resolved.Add(candidate);
        }

        // Return sorted by position
        return resolved.OrderBy(m => m.StartOffset).ToList();
    }

    internal static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var sourceLen = source.Length;
        var targetLen = target.Length;

        // Use single-row optimization
        var previousRow = new int[targetLen + 1];
        var currentRow = new int[targetLen + 1];

        for (int j = 0; j <= targetLen; j++)
            previousRow[j] = j;

        for (int i = 1; i <= sourceLen; i++)
        {
            currentRow[0] = i;
            for (int j = 1; j <= targetLen; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[targetLen];
    }

    private static HashSet<string> GetNgrams(string text, int n)
    {
        var ngrams = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i <= text.Length - n; i++)
        {
            ngrams.Add(text.Substring(i, n));
        }

        return ngrams;
    }
}
