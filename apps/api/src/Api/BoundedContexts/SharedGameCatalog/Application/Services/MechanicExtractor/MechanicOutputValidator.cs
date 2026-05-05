using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// M1.2 stub implementation of <see cref="IMechanicOutputValidator"/>. Enforces the
/// cheap-to-check guardrails from ADR-051:
/// <list type="bullet">
/// <item><description><b>T1 (quote cap)</b>: every <c>citation.quote</c> must be ≤ 25 words.</description></item>
/// <item><description><b>T4 (citation required)</b>: any object carrying a <c>claim</c>/<c>description</c>/<c>text</c>/<c>answer</c> field must have a non-empty <c>citations</c> array alongside it at the same level, unless it is the top-level section envelope.</description></item>
/// <item><description><b>JSON well-formedness</b>: output must parse as JSON.</description></item>
/// </list>
/// The deeper checks (T2 long-verbatim detection, T3 semantic grounding) require access
/// to the retrieved chunks and ship in M1.3 as a follow-up.
/// </summary>
internal sealed class MechanicOutputValidator : IMechanicOutputValidator
{
    private const int MaxQuoteWords = 25;

    public MechanicValidationResult Validate(MechanicSection section, string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return MechanicValidationResult.Invalid(new[]
            {
                new MechanicValidationViolation("well_formed", "Output is empty or whitespace.")
            });
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson);
        }
        catch (JsonException ex)
        {
            return MechanicValidationResult.Invalid(new[]
            {
                new MechanicValidationViolation("well_formed", $"Output is not valid JSON: {ex.Message}")
            });
        }

        var violations = new List<MechanicValidationViolation>();
        try
        {
            WalkAndValidate(doc.RootElement, path: "$", violations);
        }
        finally
        {
            doc.Dispose();
        }

        return violations.Count == 0
            ? MechanicValidationResult.Valid()
            : MechanicValidationResult.Invalid(violations);
    }

    private static void WalkAndValidate(JsonElement element, string path, List<MechanicValidationViolation> violations)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(element, path, violations);
                foreach (var property in element.EnumerateObject())
                {
                    WalkAndValidate(property.Value, $"{path}.{property.Name}", violations);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    WalkAndValidate(item, $"{path}[{index}]", violations);
                    index++;
                }
                break;
        }
    }

    private static void ValidateObject(JsonElement obj, string path, List<MechanicValidationViolation> violations)
    {
        // T1: quote word cap
        if (obj.TryGetProperty("quote", out var quoteElement) && quoteElement.ValueKind == JsonValueKind.String)
        {
            var quote = quoteElement.GetString();
            if (!string.IsNullOrWhiteSpace(quote))
            {
                var wordCount = CountWords(quote);
                if (wordCount > MaxQuoteWords)
                {
                    violations.Add(new MechanicValidationViolation(
                        Rule: "T1_quote_cap",
                        Message: $"Citation quote has {wordCount} words, exceeds cap of {MaxQuoteWords}.",
                        Path: $"{path}.quote"));
                }
            }
        }

        // T4: citation required for claim-bearing fields
        var claimFields = new[] { "claim", "description", "text", "answer", "primary" };
        foreach (var field in claimFields)
        {
            if (obj.TryGetProperty(field, out var claimElement)
                && claimElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(claimElement.GetString()))
            {
                if (!HasNonEmptyCitations(obj))
                {
                    violations.Add(new MechanicValidationViolation(
                        Rule: "T4_citation_required",
                        Message: $"Field '{field}' is a claim but no non-empty 'citations' array is present at the same level.",
                        Path: $"{path}.{field}"));
                }
                break; // one violation per object is enough
            }
        }
    }

    private static bool HasNonEmptyCitations(JsonElement obj)
    {
        if (!obj.TryGetProperty("citations", out var citations))
        {
            return false;
        }
        return citations.ValueKind == JsonValueKind.Array && citations.GetArrayLength() > 0;
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (inWord) count++;
                inWord = false;
            }
            else
            {
                inWord = true;
            }
        }
        if (inWord) count++;
        return count;
    }
}
