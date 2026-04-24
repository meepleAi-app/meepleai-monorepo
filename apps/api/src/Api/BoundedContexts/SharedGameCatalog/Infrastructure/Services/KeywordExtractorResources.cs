using System.Collections.Frozen;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Curated IT+EN stopwords for mechanic claim keyword extraction (Sprint 1 MVP).
/// Entries are lowercase; comparison is <see cref="StringComparer.Ordinal"/> because
/// inputs are lowercased via <c>ToLowerInvariant()</c> before lookup (avoiding the
/// case-fold cost on the hot path). Not intended to be exhaustive — curated for
/// high-frequency words that carry no mechanic signal.
/// </summary>
internal static class KeywordExtractorResources
{
    internal static readonly FrozenSet<string> Stopwords = new HashSet<string>(StringComparer.Ordinal)
    {
        // English
        "the", "a", "an", "and", "or", "but", "is", "are", "was", "were",
        "be", "been", "being", "have", "has", "had", "do", "does", "did",
        "will", "would", "could", "should", "may", "might", "must", "can",
        "to", "of", "in", "on", "at", "for", "by", "with", "from", "as",
        "that", "this", "these", "those", "it", "its", "he", "she", "they",
        "them", "we", "our", "you", "your", "my", "i", "me", "us", "his",
        "her", "their", "not", "no", "yes", "if", "then", "than", "so",
        "into", "out", "up", "down", "over", "under", "between", "about",
        "which", "who", "whom", "whose", "what", "when", "where", "why", "how",

        // Italian
        "il", "la", "le", "lo", "gli", "un", "una", "uno",
        "e", "o", "ma", "che", "di", "da", "del", "della", "delle",
        "dei", "degli", "al", "alla", "alle", "ai", "agli", "su",
        "con", "per", "come", "questo", "questa", "questi", "queste",
        "quello", "quella", "quelli", "quelle", "è", "sono", "era", "erano",
        "essere", "stato", "stata", "ha", "ho", "hai", "avere", "aveva",
        "sarà", "può", "deve", "non", "più", "meno", "tutto", "tutti",
        "tutta", "tutte", "se", "quando", "dove", "perché", "chi", "cosa",
        "qui", "là", "ora", "allora", "sul", "sulla", "sui", "sugli",
        "sulle", "nel", "nella", "nei", "negli", "nelle", "tra", "fra",
        "ne", "ci", "si", "mi", "ti", "vi", "lui", "lei", "loro", "noi", "voi",
    }.ToFrozenSet(StringComparer.Ordinal);
}
