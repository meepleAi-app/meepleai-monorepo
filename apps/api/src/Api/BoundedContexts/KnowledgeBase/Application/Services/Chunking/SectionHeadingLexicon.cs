using System.Collections.Frozen;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Epic #3338 WP1a: a curated, multilingual set of ALL-CAPS board-game rulebook section titles that
/// <c>unstructured</c>'s 'fast' extraction strategy frequently FAILS to emit as standalone
/// <c>Title</c> elements on complex multi-column layouts — instead gluing them into the body of the
/// preceding section (the Terraforming Mars "PREPARAZIONE" case: absorbed into a chunk headed
/// "CARTE"). <see cref="EmbeddedTitleSplitter"/> scans body elements for these tokens and promotes an
/// embedded occurrence to a synthetic <c>Title</c> so the heading-aware pipeline (and the #3270
/// heading-match boost + the RoleClassifier Setup fast-path) can see it.
/// <para>
/// Entries are matched CASE-SENSITIVE in their exact UPPERCASE form (so lowercase prose like
/// "during setup" never triggers), whole-word, and only when the surrounding context looks like a
/// heading (see <see cref="EmbeddedTitleSplitter"/> guards). The set is deliberately small and
/// high-value (the section types that matter for retrieval); extend it as new failing sections are
/// observed. Seeded from the SP1 structured-extraction design (§ expected section lexicon) plus the
/// TM/observed corpus.
/// </para>
/// </summary>
internal static class SectionHeadingLexicon
{
    /// <summary>
    /// Curated ALL-CAPS section titles. Multi-word entries use single ASCII spaces between words and
    /// are matched verbatim. Ordinal (case-sensitive) comparison — entries MUST be uppercase.
    /// </summary>
    public static readonly FrozenSet<string> Titles = new[]
    {
        // --- Italiano ---
        "PREPARAZIONE",
        "ALLESTIMENTO",
        "PUNTEGGIO",
        "PUNTEGGI",
        "CONTEGGIO DEI PUNTI",
        "FINE DEL GIOCO",
        "FINE DELLA PARTITA",
        "FINE PARTITA",
        "AZIONI",
        "SVOLGIMENTO",
        "SVOLGIMENTO DEL GIOCO",
        "SVOLGIMENTO DELLA PARTITA",
        "PANORAMICA",
        "SCOPO DEL GIOCO",
        "OBIETTIVO",

        // --- English ---
        "SETUP",
        "GAME SETUP",
        "SCORING",
        "END OF GAME",
        "END OF THE GAME",
        "GAME END",
        "ENDING THE GAME",
        "GAMEPLAY",
        "OBJECT OF THE GAME",
        "OVERVIEW",
        "WINNING THE GAME",
    }.ToFrozenSet(StringComparer.Ordinal);
}
