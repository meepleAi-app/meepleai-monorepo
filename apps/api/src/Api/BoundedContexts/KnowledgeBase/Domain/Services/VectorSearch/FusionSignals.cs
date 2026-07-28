using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Pure, stateless retrieval-ranking signals shared by every hybrid-fusion path
/// (issue #3270). Moved out of <c>HybridSearchService</c> so the primary chat path
/// and the admin playground apply identical legend-demotion + role-boost.
/// </summary>
internal static class FusionSignals
{
    /// <summary>Additive role-match boost (issue #1391 Phase D6).</summary>
    internal const float RoleMatchBoost = 0.15f;

    /// <summary>Additive heading-match boost (#3270). Same magnitude/shape as the role boost.</summary>
    internal const float HeadingMatchBoost = 0.15f;

    /// <summary>Punctuation/whitespace separators for <see cref="ExtractHeadingMatchTerms"/> (CA1861: hoisted).</summary>
    private static readonly char[] HeadingTermSeparators =
        { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '?', '!', '"', '(', ')' };

    /// <summary>Default reciprocal-rank-fusion constant.</summary>
    internal const int DefaultRrfK = 60;

    // Matches cross-reference pointers like "vedi pag. 10", "vedi anche pagina 5", "see p. 11",
    // "cfr. pagg. 4-5". Allows an optional connector word ("anche"/"a") and spelled-out "pagina".
    private static readonly Regex CrossReferencePointer = new(
        @"(?i)\b(?:vedi|see|cfr|cf)\b\.?\s+(?:anche\s+|a\s+)?(?:pagine|pagina|pagg|pag|pages|page|pp|p)\b\.?",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Additive boost when the query's role hint overlaps a chunk's role tags.
    /// Verbatim move of HybridSearchService.ComputeRoleMatchBoost (:536-544).
    /// </summary>
    internal static float ComputeRoleMatchBoost(GameBookRole queryRoleHint, GameBookRole chunkRoleTags)
    {
        if (queryRoleHint == GameBookRole.None || chunkRoleTags == GameBookRole.None)
        {
            return 0f;
        }

        return (chunkRoleTags & queryRoleHint) != GameBookRole.None ? RoleMatchBoost : 0f;
    }

    /// <summary>
    /// Additive boost when any normalized query term appears in the chunk's heading (#3270).
    /// Query-dependent, structural twin of <see cref="ComputeRoleMatchBoost"/>. <paramref name="queryTerms"/>
    /// MUST be lowercased (contract of <see cref="ExtractHeadingMatchTerms"/>); the heading is lowercased here.
    /// </summary>
    internal static float ComputeHeadingMatchBoost(IReadOnlyList<string>? queryTerms, string? chunkHeading)
    {
        if (queryTerms is null || queryTerms.Count == 0 || string.IsNullOrWhiteSpace(chunkHeading))
        {
            return 0f;
        }

        var headingLower = chunkHeading.ToLowerInvariant();
        for (var i = 0; i < queryTerms.Count; i++)
        {
            var term = queryTerms[i];
            if (term.Length >= 3 && headingLower.Contains(term, StringComparison.Ordinal))
            {
                return HeadingMatchBoost;
            }
        }

        return 0f;
    }

    /// <summary>
    /// Normalizes a raw query into heading-match terms: lowercased, punctuation-split, length ≥ 3,
    /// de-duplicated (order-preserving). This is the ONLY producer of the query-term contract consumed
    /// by <see cref="ComputeHeadingMatchBoost"/>.
    /// </summary>
    internal static IReadOnlyList<string> ExtractHeadingMatchTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var terms = new List<string>();
        foreach (var raw in query.Split(HeadingTermSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = raw.Trim().ToLowerInvariant();
            if (t.Length >= 3 && seen.Add(t))
            {
                terms.Add(t);
            }
        }

        return terms;
    }

    /// <summary>
    /// Number-noise demotion factor in [0, 0.5] (epic #3338 WP1b). Demotes SHORT, digit-dominated
    /// chunks that unstructured emits from tables / cross-column number fragments (the Terraforming
    /// Mars <c>"CARTE 2 5 6 4 3"</c> chunk that out-ranked the real setup prose under ts_rank_cd).
    /// <para>
    /// Multiplicative and capped at 0.5, mirroring <see cref="ComputeLegendPenaltyFactor"/>. The factor
    /// rises with the digit ratio (digits / letters+digits) but tapers with length, so a long,
    /// legitimately number-dense section (a scoring table, a component list) is barely touched while a
    /// short mostly-digits fragment is halved. Prose that merely cites a few numbers
    /// (<c>"da 2 a 5 giocatori"</c>) has a low digit ratio and is not demoted at all.
    /// </para>
    /// </summary>
    internal static float ComputeNumberNoiseFactor(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0f;
        }

        // Ratio is over digits belonging to PURELY-NUMERIC whitespace-delimited tokens ("2", "56)",
        // "3.") only — NOT digits embedded in an alphanumeric token. A GUID/id like
        // "vector-content-a1b2c3d4-2" is one letter-bearing token, so it contributes zero number-noise
        // even though it is digit-heavy; the target is space-separated number runs ("CARTE 2 5 6 4 3").
        var numberChars = 0;
        var alnum = 0;
        var tokDigits = 0;
        var tokHasLetter = false;
        var tokHasDigit = false;

        void FlushToken()
        {
            if (tokHasDigit && !tokHasLetter)
            {
                numberChars += tokDigits;
            }
            tokDigits = 0;
            tokHasLetter = false;
            tokHasDigit = false;
        }

        foreach (var ch in content)
        {
            if (char.IsWhiteSpace(ch))
            {
                FlushToken();
                continue;
            }

            if (char.IsDigit(ch))
            {
                tokDigits++;
                tokHasDigit = true;
                alnum++;
            }
            else if (char.IsLetter(ch))
            {
                tokHasLetter = true;
                alnum++;
            }
            // punctuation inside a token (".", ")", "-") is ignored: "56)" stays a number token
        }
        FlushToken();

        if (numberChars == 0 || alnum == 0)
        {
            return 0f;
        }

        var numberRatio = (double)numberChars / alnum;
        // Letters-majority text is real prose (a paragraph citing a few numbers), not number-noise.
        if (numberRatio < 0.30)
        {
            return 0f;
        }

        // Taper by length: 1.0 up to ~200 chars (MinChunkSize), shrinking beyond so a long number table
        // (legit content) is not over-demoted the way a short fragment is.
        var lengthTaper = Math.Min(1.0, 200.0 / content.Length);
        return (float)Math.Min(0.5, numberRatio * lengthTaper);
    }

    /// <summary>
    /// Legend-demotion factor in [0, 0.5] (verbatim move of :560-580).
    /// </summary>
    internal static float ComputeLegendPenaltyFactor(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0f;
        }

        var pointers = CrossReferencePointer.Count(content);
        // A legend is a LIST of pointers; a single incidental page reference is not one.
        if (pointers < 2)
        {
            return 0f;
        }

        // Density-driven ONLY (pointers per 1000 chars), never the raw count: a short chunk that
        // is mostly pointers (a legend) has high density and is capped at 0.5, while a long,
        // substantive section that legitimately cites several pages has low density and is barely
        // touched. Using the raw count would over-demote long real content with many references.
        var density = pointers * 1000.0 / content.Length;
        return (float)Math.Min(0.5, 0.05 * density);
    }
}
