using System.Text;
using System.Text.RegularExpressions;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// Il criterio di match del banco (#4434 a): come si decide che un testo atteso è «stato ritrovato».
/// </summary>
/// <remarks>
/// <para>
/// <b>Normalizzazione</b> (uguale per tutti gli estrattori, così il confronto non favorisce nessuno):
/// NFKC (le ligature <c>ﬁ</c> tornano <c>fi</c>), maiuscole/minuscole azzerate, trattino morbido
/// tolto, trattini e apici tipografici ricondotti ai caratteri semplici, spazi bianchi
/// (a capo e tabulazioni compresi) ridotti a uno. <b>Le diacritiche restano</b>: «über» e «uber», o
/// «perché» e «perche», sono parole diverse in tedesco e in italiano, e un OCR che le perde deve
/// pagarlo nella metrica.
/// </para>
/// <para>
/// <b>Tolleranza OCR</b>: rapporto massimo fra distanza di edit e lunghezza della parola più lunga
/// della coppia, applicato parola per parola (solo in <see cref="ExtractionMetrics.ScanWordRecall"/>).
/// Il valore di partenza è <c>0</c> (parola identica dopo la normalizzazione): il richiamo assorbe già
/// gli errori d'OCR come parole mancate. Una tolleranza maggiore di zero va introdotta solo con una
/// misura di <c>hi_res</c> che la giustifichi, non a mente.
/// </para>
/// </remarks>
internal static partial class ExtractionTextMatcher
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var composed = text.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(composed.Length);
        foreach (var ch in composed)
        {
            switch (ch)
            {
                case '­':
                    break; // trattino morbido: invisibile, non fa parte della parola
                case '‐' or '‑' or '‒' or '–' or '—' or '−':
                    sb.Append('-');
                    break;
                case '‘' or '’' or '‛':
                    sb.Append('\'');
                    break;
                case '“' or '”':
                    sb.Append('"');
                    break;
                default:
                    sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return WhitespaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>
    /// La forma normalizzata <b>senza spazi</b>. Serve ai confronti di contenuto («c'è, nell'ordine
    /// giusto?»): fino a #4932 Docnet restituiva il testo dopo <c>PdfTextProcessingDomainService.NormalizeText</c>,
    /// il cui passo 3 (<c>([a-z])\n([a-z])</c> → <c>$1$2</c>) incollava due righe consecutive quando la prima
    /// finiva e la seconda cominciava con una minuscola ASCII («die folgende» → «diefolgende»); il passo è tolto,
    /// ma un confronto sensibile agli spazi darebbe ancora zero a un testo che c'è tutto se un estrattore o una
    /// normalizzazione tornassero a perderne. Gli spazi mancanti si misurano a parte, con
    /// <see cref="ExtractionMetrics.WordRecall"/>.
    /// </summary>
    public static string Squeeze(string? text) =>
        Normalize(text).Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>True se <paramref name="normalizedPhrase"/> compare, contigua, in <paramref name="normalizedText"/>.</summary>
    public static bool ContainsPhrase(string normalizedText, string normalizedPhrase) =>
        normalizedPhrase.Length > 0 && normalizedText.Contains(normalizedPhrase, StringComparison.Ordinal);

    /// <summary>Le parole (sequenze di lettere o cifre) di un testo già normalizzato.</summary>
    public static HashSet<string> Words(string normalizedText)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in WordRegex().Matches(normalizedText))
        {
            words.Add(m.Value);
        }

        return words;
    }

    /// <summary>
    /// True se <paramref name="word"/> è fra le parole osservate, o ne dista al più
    /// <paramref name="tolerance"/> (rapporto di edit sulla parola più lunga della coppia).
    /// </summary>
    public static bool WordMatches(string word, HashSet<string> observedWords, double tolerance)
    {
        if (observedWords.Contains(word))
        {
            return true;
        }

        if (tolerance <= 0)
        {
            return false;
        }

        foreach (var candidate in observedWords)
        {
            var longest = Math.Max(word.Length, candidate.Length);
            var allowed = (int)Math.Floor(tolerance * longest);
            if (allowed > 0 && Math.Abs(word.Length - candidate.Length) <= allowed
                && EditDistance(word, candidate) <= allowed)
            {
                return true;
            }
        }

        return false;
    }

    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WordRegex();
}
