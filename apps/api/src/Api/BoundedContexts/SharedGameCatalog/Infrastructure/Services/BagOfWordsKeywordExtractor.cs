using Api.BoundedContexts.SharedGameCatalog.Domain.Services;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Sprint 1 MVP bag-of-words keyword extractor for mechanic claim matching.
/// </summary>
/// <remarks>
/// Algorithm:
/// <list type="number">
///   <item>Null / empty / whitespace input returns an empty array.</item>
///   <item>Input is lowercased via <c>ToLowerInvariant()</c>.</item>
///   <item>Every character that is neither a letter nor a digit is replaced with a single space.</item>
///   <item>Tokens are split on space, filtered to <c>length &gt;= 3</c>, and stopwords
///         (see <see cref="KeywordExtractorResources.Stopwords"/>) are removed.</item>
///   <item>The result is deduplicated, sorted with <see cref="StringComparer.Ordinal"/>, and returned.</item>
/// </list>
/// MVP caveats: no stemming, no language detection, no TF-IDF. Italian and English
/// stopwords are pooled into a single set because rulebook prose routinely mixes both.
/// </remarks>
public sealed class BagOfWordsKeywordExtractor : IKeywordExtractor
{
    public string[] Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var lowered = text.ToLowerInvariant();
        var buffer = new char[lowered.Length];
        // Iterates UTF-16 code units (char), not Unicode runes. Non-BMP characters
        // encoded as surrogate pairs are replaced with spaces rather than preserved,
        // because char.IsLetter / char.IsDigit return false for unpaired surrogates.
        // Acceptable for the IT+EN rulebook domain; switch to StringInfo /
        // string.EnumerateRunes if non-BMP input ever becomes relevant.
        for (var i = 0; i < lowered.Length; i++)
        {
            var c = lowered[i];
            buffer[i] = (char.IsLetter(c) || char.IsDigit(c)) ? c : ' ';
        }

        var normalized = new string(buffer);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Length < 3)
            {
                continue;
            }

            if (KeywordExtractorResources.Stopwords.Contains(token))
            {
                continue;
            }

            result.Add(token);
        }

        var array = result.ToArray();
        Array.Sort(array, StringComparer.Ordinal);
        return array;
    }
}
