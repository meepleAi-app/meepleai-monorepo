using System.Text;
using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// T4 — every citation's <c>pdf_page</c> must be within the PDF page range, and its <c>quote</c>
/// must be a normalized substring of at least one source chunk covering that page (or any chunk
/// when no page metadata is available).
/// </summary>
internal sealed class PageSubstringGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T4";
    public int Order => 20;

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();

        var normalizedChunksByPage = context.SourceChunks
            .Where(c => c.PageNumber.HasValue)
            .GroupBy(c => c.PageNumber!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => Normalize(c.Content)).ToList());
        var allNormalized = context.SourceChunks.Select(c => Normalize(c.Content)).ToList();

        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            if (!obj.TryGetProperty("pdf_page", out var pageEl) || pageEl.ValueKind != JsonValueKind.Number)
            {
                return;
            }
            if (!pageEl.TryGetInt32(out var page))
            {
                return;
            }

            if (page < 1 || (context.PdfPageCount is int pc && page > pc))
            {
                violations.Add(new MechanicValidationViolation(
                    "T4_page_out_of_range",
                    $"expected 1..{context.PdfPageCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}, got {page}",
                    $"{path}.pdf_page"));
                return;
            }

            if (!obj.TryGetProperty("quote", out var qEl) || qEl.ValueKind != JsonValueKind.String)
            {
                return;
            }
            var quote = Normalize(qEl.GetString() ?? string.Empty);
            if (quote.Length == 0)
            {
                return;
            }

            // Verify against chunks covering the cited page. Three cases:
            //  - page has indexed chunks → check those
            //  - pool HAS page metadata but not for this page (partial indexing) → unverifiable, skip.
            //    Do NOT widen to the whole document, or a fabricated page citation could pass when the
            //    quoted text happens to appear on another page.
            //  - pool has NO page metadata at all → best-effort whole-document check.
            List<string> candidates;
            if (normalizedChunksByPage.TryGetValue(page, out var byPage) && byPage.Count > 0)
            {
                candidates = byPage;
            }
            else if (normalizedChunksByPage.Count > 0)
            {
                return; // page in range but not indexed — unverifiable, skip (no false positive)
            }
            else
            {
                candidates = allNormalized;
            }
            if (candidates.Count == 0)
            {
                return; // no source pool to verify against (e.g. unit context) — skip, not a violation
            }

            var found = candidates.Any(c => c.Contains(quote, StringComparison.Ordinal));
            if (!found)
            {
                var expected = candidates[0];
                violations.Add(new MechanicValidationViolation(
                    "T4_quote_not_substring",
                    $"quote not found on page {page}. expected≈\"{Truncate(expected, 80)}\" actual=\"{Truncate(quote, 80)}\"",
                    $"{path}.quote"));
            }
        });

        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    /// <summary>Lowercase, strip punctuation/symbols, collapse whitespace to single spaces.</summary>
    internal static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        var lastSpace = false;
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastSpace = false;
            }
            else if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                if (!lastSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    lastSpace = true;
                }
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
