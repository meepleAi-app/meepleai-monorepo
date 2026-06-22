using System.Text.RegularExpressions;
using Api.BoundedContexts.DocumentProcessing.Application.Services;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Regex-based <see cref="IParagraphNumberExtractor"/> covering the numbering
/// conventions seen across Italian + English gamebook publishers.
///
/// Supported patterns (each anchored to start-of-line, multiline mode):
/// <list type="bullet">
///   <item><c>42.</c> — naked number + period (Catan-style rulebooks, common
///         gamebooks).</item>
///   <item><c>Paragrafo 42</c> / <c>Paragraph 42</c> — explicit label, IT+EN.</item>
///   <item><c>§ 42</c> / <c>§42</c> — section glyph (legal-style rulebooks).</item>
///   <item><c>(42)</c> — parenthesised header (rare; mostly choose-your-own
///         adventure variants).</item>
/// </list>
///
/// Out of scope (false positives this extractor accepts deliberately to keep
/// recall high; tuned for ≥ 80 % accuracy on the test set, not 100 %):
/// <list type="bullet">
///   <item>Numbered list items in the middle of a rule body
///         (e.g. "<c>1. Roll dice 2. Resolve</c>" on a single line) — the
///         line-anchored regex rejects these.</item>
///   <item>Page footers like "<c>42</c>" centered alone — caught as a paragraph
///         header. Low impact: a footer 42 still routes the caller to a
///         relevant page.</item>
/// </list>
///
/// Stateless and thread-safe. Patterns are compiled once at startup.
/// </summary>
internal sealed class RegexParagraphNumberExtractor : IParagraphNumberExtractor
{
    // Each pattern captures the narrative paragraph number via an explicit
    // named group; bare `(...)` are non-capturing to satisfy MA0023.
    // RegexOptions.Multiline makes `^` / `$` match per-line, not per-input.
    // ExplicitCapture: only named groups capture (MA0023 compliance).
    // Compiled options trade startup cost for per-call speed (called on every
    // indexed page; tens of thousands of pages across the catalogue).
    private const RegexOptions Options =
        RegexOptions.Compiled
        | RegexOptions.Multiline
        | RegexOptions.IgnoreCase
        | RegexOptions.CultureInvariant
        | RegexOptions.ExplicitCapture;

    // Per-call timeout to satisfy MA0009 (regex ReDoS hardening). Patterns are
    // already linear (no nested quantifiers, no alternation overlap on the same
    // span), but the timeout is cheap insurance against future edits.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    // Bound the captured number to 6 digits (max 999_999): gamebooks rarely
    // exceed 4 digits; the bound rejects accidental phone-number / year matches.
    // Named group `n` is the only capturing group (ExplicitCapture).
    private static readonly Regex[] Patterns =
    [
        // "42.\s" or "  42.\t"
        new(@"^\s*(?<n>\d{1,6})\.\s", Options, MatchTimeout),
        // "Paragrafo 42" / "paragraph 42" — IT + EN label, label is non-capturing.
        new(@"^\s*(?:Paragrafo|Paragraph)\s+(?<n>\d{1,6})\b", Options, MatchTimeout),
        // "§42" or "§ 42"
        new(@"^\s*§\s*(?<n>\d{1,6})\b", Options, MatchTimeout),
        // "(42)"
        new(@"^\s*\(\s*(?<n>\d{1,6})\s*\)", Options, MatchTimeout),
    ];

    // Hard cap to defend against OCR noise that would spray thousands of
    // matches; gamebook pages rarely carry more than ~50 paragraphs.
    private const int MaxParagraphsPerPage = 200;

    public int[] Extract(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return [];

        // Use a HashSet for de-duplication; sort once at the end.
        var seen = new HashSet<int>(capacity: 16);

        foreach (var pattern in Patterns)
        {
            foreach (Match match in pattern.Matches(ocrText))
            {
                if (seen.Count >= MaxParagraphsPerPage)
                    break;

                if (!match.Success)
                    continue;

                var numberGroup = match.Groups["n"];
                if (!numberGroup.Success)
                    continue;

                if (int.TryParse(
                        numberGroup.ValueSpan,
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var number)
                    && number >= 1)
                {
                    seen.Add(number);
                }
            }

            if (seen.Count >= MaxParagraphsPerPage)
                break;
        }

        if (seen.Count == 0)
            return [];

        var result = new int[seen.Count];
        seen.CopyTo(result);
        Array.Sort(result);
        return result;
    }
}
