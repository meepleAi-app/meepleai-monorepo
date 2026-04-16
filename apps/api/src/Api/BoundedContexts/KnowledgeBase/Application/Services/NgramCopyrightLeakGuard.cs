using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Detects verbatim copyright leak by scanning response bodies for consecutive-word
/// runs matching any Protected chunk's full text (or preview fallback).
///
/// Algorithm: case-insensitive, Unicode-punctuation-normalized token comparison.
/// Complexity: O(|body| × |source| × |chunks| × N) worst case — &lt;50ms for typical payloads
/// (5 chunks × 1500 tokens × 500 body tokens × N=12 ≈ 45M ops, measured ~23ms).
///
/// Configuration: VerbatimRunThreshold (default 12), ScanTimeoutMs (default 500).
/// </summary>
internal sealed partial class NgramCopyrightLeakGuard : ICopyrightLeakGuard
{
    [GeneratedRegex(@"[\p{P}\p{Z}\s]+", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenSplitPattern();

    private readonly CopyrightLeakGuardOptions _options;
    private readonly ILogger<NgramCopyrightLeakGuard> _logger;

    public NgramCopyrightLeakGuard(
        IOptions<CopyrightLeakGuardOptions> options,
        ILogger<NgramCopyrightLeakGuard> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<CopyrightLeakResult> ScanAsync(
        string responseBody,
        IReadOnlyList<ChunkCitation> protectedCitations,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(responseBody) || protectedCitations.Count == 0)
        {
            return Task.FromResult(new CopyrightLeakResult(false, Array.Empty<LeakMatch>()));
        }

        var n = _options.VerbatimRunThreshold;
        var bodyTokens = Tokenize(responseBody);
        if (bodyTokens.Length < n)
        {
            return Task.FromResult(new CopyrightLeakResult(false, Array.Empty<LeakMatch>()));
        }

        var matches = new List<LeakMatch>();

        foreach (var citation in protectedCitations)
        {
            ct.ThrowIfCancellationRequested();

            var source = citation.FullText ?? citation.SnippetPreview;
            if (string.IsNullOrWhiteSpace(source))
            {
                _logger.LogWarning(
                    "Skipping scan for citation {DocumentId}:{PageNumber} — FullText and SnippetPreview both empty",
                    citation.DocumentId, citation.PageNumber);
                continue;
            }

            var sourceTokens = Tokenize(source);
            if (sourceTokens.Length < n) continue;

            var match = FindFirstRun(bodyTokens, sourceTokens, n, ct);
            if (match is not null)
            {
                var actualLength = ExtendRun(bodyTokens, sourceTokens, match.Value.BodyIndex, match.Value.SourceIndex, n);
                matches.Add(new LeakMatch(
                    DocumentId: citation.DocumentId,
                    PageNumber: citation.PageNumber,
                    RunLength: actualLength,
                    BodyStartIndex: match.Value.BodyIndex,
                    MatchedText: string.Join(' ', bodyTokens, match.Value.BodyIndex, actualLength)));
            }
        }

        return Task.FromResult(new CopyrightLeakResult(matches.Count > 0, matches));
    }

    private static string[] Tokenize(string text)
    {
        var lowered = text.ToLowerInvariant();
        return TokenSplitPattern()
            .Split(lowered)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
    }

    /// <summary>
    /// Finds the first run of N consecutive tokens in body that appears
    /// anywhere (as a consecutive subsequence) in source.
    /// Checks cancellation token on each outer iteration for responsive timeout enforcement.
    /// Returns (BodyIndex, SourceIndex) of the match, or null if none.
    /// </summary>
    private static (int BodyIndex, int SourceIndex)? FindFirstRun(
        string[] body, string[] source, int n, CancellationToken ct)
    {
        for (int i = 0; i <= body.Length - n; i++)
        {
            ct.ThrowIfCancellationRequested();  // responsive timeout
            for (int j = 0; j <= source.Length - n; j++)
            {
                if (SequenceEqualsAt(body, i, source, j, n))
                    return (i, j);
            }
        }
        return null;
    }

    /// <summary>
    /// Greedy extension: given an initial match of length N at (bodyStart, sourceStart),
    /// returns the actual contiguous run length by extending forward while tokens match.
    /// </summary>
    private static int ExtendRun(string[] body, string[] source, int bodyStart, int sourceStart, int minLength)
    {
        int length = minLength;
        int maxExtension = Math.Min(body.Length - bodyStart, source.Length - sourceStart);
        while (length < maxExtension &&
               string.Equals(body[bodyStart + length], source[sourceStart + length], StringComparison.Ordinal))
        {
            length++;
        }
        return length;
    }

    private static bool SequenceEqualsAt(
        string[] a, int aStart, string[] b, int bStart, int length)
    {
        for (int k = 0; k < length; k++)
        {
            if (!string.Equals(a[aStart + k], b[bStart + k], StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
