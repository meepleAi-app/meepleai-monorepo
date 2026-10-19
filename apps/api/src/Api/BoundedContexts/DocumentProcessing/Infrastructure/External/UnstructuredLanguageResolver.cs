using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// #4435: decides the <c>language</c> form field sent to the Unstructured service, which used to be the
/// literal <c>"ita"</c> for every document.
/// <para>
/// Precedence: the document's <c>language_override</c>; then its detected <c>Language</c>, when
/// <c>language_confidence</c> is strictly above <see cref="UnstructuredOptions.LanguageConfidenceThreshold"/>;
/// otherwise the configured set <see cref="UnstructuredOptions.Language"/> (<c>eng+ita+deu</c> by default).
/// A document language whose Tesseract code is not in that set also gets the whole set: the set declares
/// what the service can read, and Tesseract asked for a single language it has no data for loads nothing
/// and fails the OCR.
/// </para>
/// <para>
/// Wire format: Tesseract codes joined by <c>+</c>, in one string. The service passes it as
/// <c>partition_pdf(languages=[value])</c>, and unstructured's <c>check_language_args</c> splits on
/// <c>+</c> before handing the codes to Tesseract (unstructured 0.18.18,
/// <c>unstructured/partition/common/lang.py</c>). The value only matters where OCR runs — the
/// <c>hi_res</c> strategy; <c>fast</c> reads the embedded text and uses it for element metadata only.
/// </para>
/// </summary>
internal static class UnstructuredLanguageResolver
{
    private const char Separator = '+';
    private static readonly char[] TokenSeparators = { '+', ',', ' ', '\t' };
    private static readonly char[] RegionSeparators = { '-', '_' };

    /// <summary>
    /// ISO 639-1 → Tesseract traineddata name, for the codes a <c>pdf_documents</c> row can carry: the
    /// <c>LanguageCode</c> whitelist (override) plus what <c>LanguageDetector</c> can emit.
    /// </summary>
    private static readonly Dictionary<string, string> TesseractCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "eng",
        ["it"] = "ita",
        ["de"] = "deu",
        ["fr"] = "fra",
        ["es"] = "spa",
        ["pt"] = "por",
        ["pl"] = "pol",
        ["nl"] = "nld",
        ["ja"] = "jpn",
        ["zh"] = "chi_sim",
        ["sv"] = "swe",
        ["da"] = "dan",
        ["no"] = "nor",
        ["fi"] = "fin",
        ["cs"] = "ces",
        ["hu"] = "hun",
        ["ro"] = "ron",
        ["hr"] = "hrv",
        ["el"] = "ell",
        ["ru"] = "rus",
        ["ar"] = "ara",
        ["he"] = "heb",
        ["th"] = "tha",
        ["hi"] = "hin",
    };

    public static string Resolve(DocumentLanguageHint? document, UnstructuredOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = ParseLanguageSet(options.Language);
        if (configured.Count == 0)
        {
            configured = ParseLanguageSet(UnstructuredOptions.DefaultLanguage);
        }

        var documentCode = ToTesseractCode(PickDocumentLanguage(document, options.LanguageConfidenceThreshold));
        return documentCode is not null && configured.Contains(documentCode, StringComparer.Ordinal)
            ? documentCode
            : string.Join(Separator, configured);
    }

    /// <summary>Maps an ISO 639-1 code (case and region insensitive) to its Tesseract code, or null.</summary>
    public static string? ToTesseractCode(string? iso6391)
    {
        if (string.IsNullOrWhiteSpace(iso6391))
        {
            return null;
        }

        var primary = iso6391.Trim().Split(RegionSeparators)[0];
        return TesseractCodes.TryGetValue(primary, out var code) ? code : null;
    }

    /// <summary>
    /// The configured set as distinct Tesseract codes, in order. Accepts <c>+</c>, comma or spaces as
    /// separators and ISO 639-1 codes for convenience (<c>en+it</c> → <c>eng+ita</c>), so the membership
    /// check against a document's code is made on the same spelling.
    /// </summary>
    internal static IReadOnlyList<string> ParseLanguageSet(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Array.Empty<string>();
        }

        return configured
            .Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .Select(token => token.Length == 2 ? ToTesseractCode(token) ?? token : token)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? PickDocumentLanguage(DocumentLanguageHint? document, double threshold)
    {
        if (document is null)
        {
            return null;
        }

        // The override is a human's statement about the document: when present it alone decides, even
        // if it cannot be honoured (not in the set) — falling back to the detection would contradict it.
        if (!string.IsNullOrWhiteSpace(document.LanguageOverride))
        {
            return document.LanguageOverride;
        }

        // A null confidence means "never detected": the row's Language is then the entity default "en".
        return document.DetectedConfidence is { } confidence && confidence > threshold
            ? document.DetectedLanguage
            : null;
    }
}
