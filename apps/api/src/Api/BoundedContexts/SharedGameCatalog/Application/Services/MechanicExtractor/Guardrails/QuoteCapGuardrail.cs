using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// T1 — every <c>citation.quote</c> must be ≤ <see cref="Configuration.MechanicGuardrailOptions.MaxQuoteWords"/>
/// words. Tokenizer is hardened against Unicode whitespace (NBSP, thin space), em/en dashes,
/// and pure-punctuation tokens.
/// </summary>
internal sealed class QuoteCapGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T1";
    public int Order => 10;

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.TryGetProperty("quote", out var q) && q.ValueKind == JsonValueKind.String)
            {
                var text = q.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var count = CountWords(text!);
                    if (count > context.Options.MaxQuoteWords)
                    {
                        violations.Add(new MechanicValidationViolation(
                            "T1_quote_cap",
                            $"quote has {count} words, max is {context.Options.MaxQuoteWords}",
                            $"{path}.quote"));
                    }
                }
            }
        });
        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    /// <summary>
    /// Counts word tokens: any maximal run of non-separator chars containing ≥1 letter or digit.
    /// Unicode whitespace and em/en dashes split words; pure-punctuation runs are NOT counted.
    /// </summary>
    internal static int CountWords(string text)
    {
        var count = 0;
        var i = 0;
        var n = text.Length;
        while (i < n)
        {
            while (i < n && IsSeparator(text[i]))
            {
                i++;
            }
            if (i >= n)
            {
                break;
            }

            var hasAlnum = false;
            while (i < n && !IsSeparator(text[i]))
            {
                if (char.IsLetterOrDigit(text[i]))
                {
                    hasAlnum = true;
                }
                i++;
            }
            if (hasAlnum)
            {
                count++;
            }
        }
        return count;
    }

    private static bool IsSeparator(char c) =>
        char.IsWhiteSpace(c) || c == '—' || c == '–'; // em-dash, en-dash
}
