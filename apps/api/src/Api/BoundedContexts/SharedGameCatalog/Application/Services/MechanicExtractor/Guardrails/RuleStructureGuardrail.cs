using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// T5 — structural checks on the v1.2.0 fields (spec 2026-10-08 §3): override ordinals in range and
/// not self, acyclic override graph, no Example in the graph, Exception bound by overrides or trigger,
/// trigger names present in the retrieved sources (vocabulary proxy until entity resolution lands).
/// Violations carry the item's JSONPath so CorrelateValidations attaches them to the right claim.
/// A T5 failure does not drop the claim nor trigger a retry (<see cref="IsAdvisory"/>); it flags it
/// for the reviewer.
/// </summary>
internal sealed class RuleStructureGuardrail : IMechanicGuardrail
{
    private static readonly string[] ListSections = { "mechanics", "phases", "faq", "setup", "components", "endgame", "resources" };

    private static readonly string[] TriggerFields = { "phase", "action", "component" };

    public string RuleFamily => "T5";

    public int Order => 25;

    public bool IsAdvisory => true;

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        var sourceText = string.Join('\n', context.SourceChunks.Select(c => c.Content));
        var normalizedSource = MechanicTrigger.Normalize(sourceText);

        foreach (var section in ListSections)
        {
            if (!context.Root.TryGetProperty(section, out var items) || items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            EvaluateSection(section, items, normalizedSource, violations);
        }

        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    private static void EvaluateSection(
        string section, JsonElement items, string normalizedSource, List<MechanicValidationViolation> violations)
    {
        var list = items.EnumerateArray().ToList();
        var kinds = list.Select(i => i.ValueKind != JsonValueKind.Object ? "rule" : ReadString(i, "kind")?.Trim().ToLowerInvariant() ?? "rule").ToList();
        var edges = new Dictionary<int, List<int>>();

        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item.ValueKind != JsonValueKind.Object)
            {
                edges[i] = new List<int>();
                continue;
            }

            var path = $"$.{section}[{i}]";
            var targets = new List<int>();
            var declaredOverrides = 0;

            if (item.TryGetProperty("overrides", out var ov) && ov.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in ov.EnumerateArray())
                {
                    declaredOverrides++;
                    if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var ord) || ord < 0 || ord >= list.Count || ord == i || list[ord].ValueKind != JsonValueKind.Object)
                    {
                        violations.Add(new MechanicValidationViolation("T5_override_missing",
                            $"Override ordinal '{el}' is out of range or refers to the item itself.", path));
                        continue;
                    }

                    if (IsExample(kinds[ord]) || IsExample(kinds[i]))
                    {
                        violations.Add(new MechanicValidationViolation("T5_example_in_override",
                            "An example item cannot take part in an override edge.", path));
                        continue;
                    }

                    targets.Add(ord);
                }
            }

            edges[i] = targets;

            var hasTrigger = item.TryGetProperty("trigger", out var tr) && tr.ValueKind == JsonValueKind.Object;
            if (string.Equals(kinds[i], "exception", StringComparison.Ordinal) && declaredOverrides == 0 && !hasTrigger)
            {
                violations.Add(new MechanicValidationViolation("T5_exception_unbound",
                    "An exception item needs 'overrides' or 'trigger'.", path));
            }

            if (hasTrigger)
            {
                foreach (var field in TriggerFields)
                {
                    var name = ReadString(tr, field);
                    if (!string.IsNullOrWhiteSpace(name)
                        && !normalizedSource.Contains(MechanicTrigger.Normalize(name), StringComparison.Ordinal))
                    {
                        violations.Add(new MechanicValidationViolation("T5_trigger_unknown",
                            $"Trigger {field} '{name}' does not appear in the retrieved sources.", path));
                    }
                }
            }
        }

        var cycleMember = FindCycleMember(edges);
        if (cycleMember is not null)
        {
            // Anchored on an item (not the section) so CorrelateValidations can attach it to a claim:
            // the highest-index member is the one whose outgoing edge the parser drops.
            violations.Add(new MechanicValidationViolation("T5_override_cycle",
                "The overrides graph of this section contains a cycle.", $"$.{section}[{cycleMember}]"));
        }
    }

    /// <summary>Returns the highest-index node of the first cycle found, or null when the graph is acyclic.</summary>
    private static int? FindCycleMember(Dictionary<int, List<int>> edges)
    {
        var state = new Dictionary<int, int>();
        var stack = new List<int>();

        int? Visit(int n)
        {
            if (state.TryGetValue(n, out var s))
            {
                return s == 1 ? stack.Skip(stack.IndexOf(n)).Max() : null;
            }

            state[n] = 1;
            stack.Add(n);
            foreach (var m in edges.TryGetValue(n, out var next) ? next : new List<int>())
            {
                var found = Visit(m);
                if (found is not null)
                {
                    return found;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[n] = 2;
            return null;
        }

        foreach (var n in edges.Keys)
        {
            var found = Visit(n);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsExample(string kind) => string.Equals(kind, "example", StringComparison.Ordinal);

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
