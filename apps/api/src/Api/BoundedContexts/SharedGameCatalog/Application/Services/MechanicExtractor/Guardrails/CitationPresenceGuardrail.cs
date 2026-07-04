using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// T3a — any object carrying a claim-bearing field (<c>claim</c>/<c>description</c>/<c>text</c>/
/// <c>answer</c>/<c>primary</c>) must have a non-empty <c>citations</c> array at the same level.
/// (Extracted from the M1.2 stub; rule renamed from <c>T4_citation_required</c> to
/// <c>T3_citation_required</c> per the ADR-051 / #525 naming.)
/// </summary>
internal sealed class CitationPresenceGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T3a";
    public int Order => 15;

    private static readonly string[] ClaimFields = { "claim", "description", "text", "answer", "primary" };

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            foreach (var field in ClaimFields)
            {
                if (obj.TryGetProperty(field, out var claimElement)
                    && claimElement.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(claimElement.GetString()))
                {
                    if (!HasNonEmptyCitations(obj))
                    {
                        violations.Add(new MechanicValidationViolation(
                            "T3_citation_required",
                            $"Field '{field}' is a claim but no non-empty 'citations' array is present at the same level.",
                            $"{path}.{field}"));
                    }
                    break; // one violation per object is enough
                }
            }
        });
        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    private static bool HasNonEmptyCitations(JsonElement obj)
    {
        if (!obj.TryGetProperty("citations", out var citations))
        {
            return false;
        }
        return citations.ValueKind == JsonValueKind.Array && citations.GetArrayLength() > 0;
    }
}
