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
