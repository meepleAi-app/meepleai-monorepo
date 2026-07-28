using Api.Constants;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Epic #3338 WP3: a per-game extraction-quality signal computed from the section HEADINGS a game's
/// chunks carry (<c>text_chunks.Heading</c>) — what retrieval actually sees. It measures how many
/// distinct headings are plausible section titles (vs. the garbage unstructured's 'fast' strategy
/// emits on complex layouts — spaced single letters "I L E X Y R F", single chars "D", symbol/number
/// noise "(14%), Oceani") and how many curated canonical section types are present. Its purpose is the
/// WP3 regression guard (a well-extracted game's health must not drop after a WP1/WP4 change) and the
/// WP4 go/no-go read-out (a residual IT cohort below the band threshold triggers hi_res extraction).
/// <para>Pure and allocation-light — no I/O; the caller supplies the game's chunk headings.</para>
/// </summary>
internal static class TitleHealthMetric
{
    /// <summary>
    /// A section title is short (real ones observed: "PLANCE DEI GIOCATORI"=20, "SVOLGIMENTO DELLA
    /// PARTITA"=25). Longer strings are running headers, copyright/URL lines
    /// ("© 2016 FryxGames www.fryxgames.se/…") or prose bleed.
    /// </summary>
    private const int MaxPlausibleHeadingLength = 40;

    /// <summary>Minimum fraction of non-space characters that must be letters for a heading to be plausible.</summary>
    private const double MinLetterFraction = 0.6;

    private const double GreenThreshold = 0.80;
    private const double YellowThreshold = 0.50;

    /// <param name="DistinctHeadings">Distinct non-blank headings across the game's chunks.</param>
    /// <param name="PlausibleHeadings">How many of those look like real section titles.</param>
    /// <param name="PlausibleFraction">PlausibleHeadings / DistinctHeadings (0 when there are none).</param>
    /// <param name="CanonicalCoverage">How many curated <see cref="SectionHeadingLexicon"/> section types appear.</param>
    /// <param name="Band">green ≥ 0.80 · yellow ≥ 0.50 · red otherwise — the WP4 go/no-go band.</param>
    public readonly record struct TitleHealthResult(
        int DistinctHeadings,
        int PlausibleHeadings,
        double PlausibleFraction,
        int CanonicalCoverage,
        string Band);

    public static TitleHealthResult Compute(IEnumerable<string?> chunkHeadings)
    {
        ArgumentNullException.ThrowIfNull(chunkHeadings);

        var distinct = chunkHeadings
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var plausible = distinct.Count(IsPlausibleHeading);
        var fraction = distinct.Count == 0 ? 0.0 : (double)plausible / distinct.Count;
        var canonical = CountCanonicalCoverage(distinct);
        var band = fraction >= GreenThreshold ? "green"
            : fraction >= YellowThreshold ? "yellow"
            : "red";

        return new TitleHealthResult(distinct.Count, plausible, fraction, canonical, band);
    }

    /// <summary>
    /// True when a heading looks like a real section title: 2–80 chars, carrying a real word (a run of
    /// ≥ <see cref="ChunkingConstants.MinSubstantiveLetterRun"/> consecutive letters, so "D", "S U",
    /// "I L E X Y R F" are rejected) and dominantly letters (≥ 60 % of non-space chars, so "(14%), Oceani"
    /// and "© 2016 FryxGames" are rejected). Measures title-LIKENESS, not section-correctness — a real
    /// word fragment like "Dato" counts as plausible; the target is gross extraction garbage.
    /// </summary>
    internal static bool IsPlausibleHeading(string? heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return false;
        }

        var t = heading.Trim();
        if (t.Length < 2 || t.Length > MaxPlausibleHeadingLength)
        {
            return false;
        }

        var run = 0;
        var letters = 0;
        var nonSpace = 0;
        var hasWord = false;
        foreach (var ch in t)
        {
            if (!char.IsWhiteSpace(ch))
            {
                nonSpace++;
            }

            if (char.IsLetter(ch))
            {
                letters++;
                if (++run >= ChunkingConstants.MinSubstantiveLetterRun)
                {
                    hasWord = true;
                }
            }
            else
            {
                run = 0;
            }
        }

        return hasWord && nonSpace > 0 && (double)letters / nonSpace >= MinLetterFraction;
    }

    private static int CountCanonicalCoverage(IEnumerable<string> headings)
    {
        var upperHeadings = headings.Select(h => h.ToUpperInvariant()).ToList();
        var covered = 0;
        foreach (var title in SectionHeadingLexicon.Titles)
        {
            if (upperHeadings.Any(h => ContainsWholeWord(h, title)))
            {
                covered++;
            }
        }
        return covered;
    }

    private static bool ContainsWholeWord(string text, string word)
    {
        var i = text.IndexOf(word, StringComparison.Ordinal);
        while (i >= 0)
        {
            var leftOk = i == 0 || !char.IsLetter(text[i - 1]);
            var end = i + word.Length;
            var rightOk = end >= text.Length || !char.IsLetter(text[end]);
            if (leftOk && rightOk)
            {
                return true;
            }
            i = text.IndexOf(word, i + 1, StringComparison.Ordinal);
        }
        return false;
    }
}
