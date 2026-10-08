using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Parses the nine section-level JSON envelopes emitted by the Mechanic Extractor pipeline
/// into a flat list of <see cref="MechanicClaim"/> entities ready to be attached to a
/// <see cref="Domain.Aggregates.MechanicAnalysis"/> aggregate.
/// </summary>
/// <remarks>
/// The parser is intentionally defensive:
/// <list type="bullet">
/// <item><description>Malformed JSON for a section is skipped (no claims emitted for that section)
/// rather than aborting the whole analysis — validation already ran per-section upstream.</description></item>
/// <item><description>Items lacking a non-empty <c>citations</c> array are dropped (domain factory
/// requires ≥1 citation per ADR-051 T3).</description></item>
/// <item><description>Citations whose <c>pdf_page</c> is missing/non-positive or whose <c>quote</c>
/// violates the 25-word cap are dropped before the claim factory runs, so a single bad citation
/// doesn't disqualify the rest of a well-formed item.</description></item>
/// <item><description>Claim Ids are pre-allocated so each <see cref="MechanicCitation.ClaimId"/>
/// equals the real <c>MechanicClaim.Id</c> at persistence time — matching the explicit FK
/// wiring in <c>MechanicClaimEntityConfiguration</c>. Because of this constraint we build claims
/// via <see cref="MechanicClaim.CreateWithId"/> with the pre-allocated Id (preserving
/// <c>IsNew = true</c> so the repository persists them as INSERT, not UPDATE).</description></item>
/// <item><description>List sections run in two passes (prompt v1.2.0): the first pass allocates a claim
/// Id per valid item, the second builds the claims so <c>overrides</c> ordinals — the RAW 0-based
/// position in the section array, the same index used by the <c>sourceAnchor</c> — can be resolved
/// to Ids.</description></item>
/// </list>
/// </remarks>
internal static class MechanicOutputParser
{
    /// <summary>
    /// Parses each section envelope into a flattened list of <see cref="MechanicClaim"/> entities.
    /// The returned list preserves the section order supplied in <paramref name="sectionOutputs"/>
    /// and assigns a contiguous <c>displayOrder</c> per section (0-based).
    /// </summary>
    public static IReadOnlyList<MechanicClaim> Parse(
        Guid analysisId,
        IReadOnlyDictionary<MechanicSection, string> sectionOutputs)
    {
        ArgumentNullException.ThrowIfNull(sectionOutputs);

        var claims = new List<MechanicClaim>();

        foreach (var (section, rawJson) in sectionOutputs)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(rawJson);
            }
            catch (JsonException)
            {
                // Upstream validator should have caught this; defense in depth — skip section.
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var sectionClaims = section switch
                {
                    MechanicSection.Summary => ParseSummary(analysisId, root),
                    MechanicSection.Mechanics => ParseMechanics(analysisId, root),
                    MechanicSection.Victory => ParseVictory(analysisId, root),
                    MechanicSection.Resources => ParseResources(analysisId, root),
                    MechanicSection.Phases => ParsePhases(analysisId, root),
                    MechanicSection.Faq => ParseFaq(analysisId, root),
                    MechanicSection.Setup => ParseSetup(analysisId, root),
                    MechanicSection.Components => ParseComponents(analysisId, root),
                    MechanicSection.EndgameScoring => ParseEndgame(analysisId, root),
                    _ => Array.Empty<MechanicClaim>()
                };

                claims.AddRange(sectionClaims);
            }
        }

        return claims;
    }

    // ============================================================
    // Section: Summary
    // Schema: { "summary": { "text": "...", "citations": [...] } }
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseSummary(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var text = ReadString(summary, "text");
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var claimId = Guid.NewGuid();
        var citations = ExtractCitations(summary, claimId).ToList();
        if (citations.Count == 0)
        {
            yield break;
        }

        yield return BuildClaim(
            claimId: claimId,
            analysisId: analysisId,
            section: MechanicSection.Summary,
            text: text!,
            displayOrder: 0,
            citations: citations,
            sourceAnchor: "$.summary",
            structure: MechanicClaimStructure.Default);
    }

    // ============================================================
    // Section: Mechanics
    // Schema: { "mechanics": [{ "name": "...", "description": "...", "citations": [...] }] }
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseMechanics(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("mechanics", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MechanicClaim>();
        }

        var prepared = PrepareItems(items, "description", item => LabelledText(item, "name", ReadString(item, "description")!));
        return EmitItems(analysisId, MechanicSection.Mechanics, "mechanics", prepared, sortByDeclaredOrder: false);
    }

    // ============================================================
    // Section: Victory
    // Schema: { "victory": { "primary": "...", "alternatives": ["..."], "citations": [...] } }
    // The envelope holds one citation array shared between primary and alternatives.
    // kind/priority/trigger (v1.2.0) are read from the victory object and apply to the primary;
    // `overrides` is not meaningful here (the only addressable item is the primary itself, so any
    // ordinal is dropped as self/out-of-range). Alternatives keep the default structure.
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseVictory(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("victory", out var victory) || victory.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var primary = ReadString(victory, "primary");
        if (string.IsNullOrWhiteSpace(primary))
        {
            yield break;
        }

        var displayOrder = 0;

        // Primary claim with a fresh Id and its own citation copies.
        var primaryClaimId = Guid.NewGuid();
        var primaryCitations = ExtractCitations(victory, primaryClaimId).ToList();
        if (primaryCitations.Count == 0)
        {
            yield break;
        }

        // The primary anchors to the whole "$.victory" object; its guardrail violations land on
        // "$.victory" or "$.victory.citations[n]", both of which prefix-match this anchor (shared
        // citations belong to the primary). NOTE: because MatchesAnchor is a one-way prefix check,
        // "$.victory" also prefix-covers the "$.victory.alternatives[i]" subtree. That is harmless
        // today — no guardrail walks the alternatives strings, so no "$.victory.alternatives[i]"
        // violation path is ever produced. If alternatives-level validation is ever added, give the
        // primary a boundary-exact anchor (or tighten MatchesAnchor) so it stops covering them.
        yield return BuildClaim(
            claimId: primaryClaimId,
            analysisId: analysisId,
            section: MechanicSection.Victory,
            text: primary!,
            displayOrder: displayOrder++,
            citations: primaryCitations,
            sourceAnchor: "$.victory",
            structure: ReadStructure(victory, new[] { primaryClaimId }, 0));

        // Alternatives reuse the same citation source — re-extract per claim so ClaimId wires up.
        if (!victory.TryGetProperty("alternatives", out var alternatives)
            || alternatives.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var altIndex = -1;
        foreach (var alt in alternatives.EnumerateArray())
        {
            // #2808: stamp the RAW array index so each alternative carries a stable
            // per-claim anchor ($.victory.alternatives[i]) that a "$.victory" primary
            // violation no longer prefix-matches — mirrors the $.mechanics[i] semantics.
            altIndex++;
            if (alt.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = alt.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var altClaimId = Guid.NewGuid();
            var altCitations = ExtractCitations(victory, altClaimId).ToList();
            if (altCitations.Count == 0)
            {
                continue;
            }

            yield return BuildClaim(
                claimId: altClaimId,
                analysisId: analysisId,
                section: MechanicSection.Victory,
                text: text!,
                displayOrder: displayOrder++,
                citations: altCitations,
                sourceAnchor: $"$.victory.alternatives[{altIndex}]",
                structure: MechanicClaimStructure.Default);
        }
    }

    // ============================================================
    // Section: Resources
    // Schema: { "resources": [{ "name": "...", "usage": "...", "citations": [...] }] }
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseResources(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("resources", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MechanicClaim>();
        }

        var prepared = PrepareItems(items, "usage", item => LabelledText(item, "name", ReadString(item, "usage")!));
        return EmitItems(analysisId, MechanicSection.Resources, "resources", prepared, sortByDeclaredOrder: false);
    }

    // ============================================================
    // Section: Phases
    // Schema: { "phases": [{ "name": "...", "description": "...", "order": 1, "citations": [...] }] }
    // Entries are emitted in their declared `order`; when missing or duplicate we fall back to
    // the source array order. Anchors and override ordinals stay the RAW source index.
    // ============================================================
    private static IEnumerable<MechanicClaim> ParsePhases(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("phases", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MechanicClaim>();
        }

        var prepared = PrepareItems(items, "description", item => LabelledText(item, "name", ReadString(item, "description")!));
        return EmitItems(analysisId, MechanicSection.Phases, "phases", prepared, sortByDeclaredOrder: true);
    }

    // ============================================================
    // Section: FAQ
    // Schema: { "faq": [{ "question": "...", "answer": "...", "citations": [...] }] }
    // Stored as a single claim per entry with the question prefixed when present.
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseFaq(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("faq", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MechanicClaim>();
        }

        var prepared = PrepareItems(items, "answer", item =>
        {
            var answer = ReadString(item, "answer")!;
            var question = ReadString(item, "question");
            return string.IsNullOrWhiteSpace(question)
                ? answer
                : $"Q: {question.Trim()}\nA: {answer.Trim()}";
        });
        return EmitItems(analysisId, MechanicSection.Faq, "faq", prepared, sortByDeclaredOrder: false);
    }

    // ============================================================
    // Section: Setup (v1.1.0)
    // Schema: { "setup": [{ "description": "...", "order": 1, "playerCountNote": "...", "citations": [...] }] }
    // Emitted in declared `order`; falls back to source array order when missing/duplicate.
    // Anchors and override ordinals stay the RAW source index.
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseSetup(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("setup", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MechanicClaim>();
        }

        var prepared = PrepareItems(items, "description", item =>
        {
            var description = ReadString(item, "description")!;
            var note = ReadString(item, "playerCountNote");
            return string.IsNullOrWhiteSpace(note)
                ? description
                : $"{description.Trim()} ({note.Trim()})";
        });
        return EmitItems(analysisId, MechanicSection.Setup, "setup", prepared, sortByDeclaredOrder: true);
    }

    // ============================================================
    // Section: Components (v1.1.0)
    // Schema: { "components": [{ "name": "...", "description": "...", "quantity": "...", "citations": [...] }] }
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseComponents(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("components", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MechanicClaim>();
        }

        var prepared = PrepareItems(items, "description", item =>
        {
            var description = ReadString(item, "description")!;
            var name = ReadString(item, "name");
            var quantity = ReadString(item, "quantity");
            string? label;
            if (string.IsNullOrWhiteSpace(quantity))
            {
                label = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            }
            else if (string.IsNullOrWhiteSpace(name))
            {
                label = $"×{quantity.Trim()}";
            }
            else
            {
                label = $"{name.Trim()} (×{quantity.Trim()})";
            }

            return string.IsNullOrWhiteSpace(label)
                ? description
                : $"{label}: {description.Trim()}";
        });
        return EmitItems(analysisId, MechanicSection.Components, "components", prepared, sortByDeclaredOrder: false);
    }

    // ============================================================
    // Section: EndgameScoring (v1.1.0)
    // Schema: { "endgame": [{ "name": "...", "description": "...", "citations": [...] }] }
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseEndgame(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("endgame", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MechanicClaim>();
        }

        var prepared = PrepareItems(items, "description", item => LabelledText(item, "name", ReadString(item, "description")!));
        return EmitItems(analysisId, MechanicSection.EndgameScoring, "endgame", prepared, sortByDeclaredOrder: false);
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>One valid list item after the first (id-allocation) pass.</summary>
    private sealed record PreparedItem(
        int SourceIndex,
        JsonElement Element,
        Guid ClaimId,
        IReadOnlyList<MechanicCitation> Citations,
        string Text,
        int? Order);

    /// <summary>
    /// First pass over a list section: allocates a claim Id for every RAW item that will become a
    /// claim. The result has exactly one slot per raw array item; skipped items (non-object,
    /// missing <paramref name="requiredField"/>, no valid citations) are <c>null</c>.
    /// </summary>
    private static IReadOnlyList<PreparedItem?> PrepareItems(
        JsonElement items, string requiredField, Func<JsonElement, string> buildText)
    {
        var prepared = new List<PreparedItem?>();
        var sourceIndex = 0;
        foreach (var item in items.EnumerateArray())
        {
            prepared.Add(PrepareItem(item, sourceIndex, requiredField, buildText));
            sourceIndex++;
        }

        return prepared;
    }

    private static PreparedItem? PrepareItem(
        JsonElement item, int sourceIndex, string requiredField, Func<JsonElement, string> buildText)
    {
        if (item.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(ReadString(item, requiredField)))
        {
            return null;
        }

        var claimId = Guid.NewGuid();
        var citations = ExtractCitations(item, claimId).ToList();
        if (citations.Count == 0)
        {
            return null;
        }

        int? order = null;
        if (item.TryGetProperty("order", out var orderEl)
            && orderEl.ValueKind == JsonValueKind.Number
            && orderEl.TryGetInt32(out var parsedOrder))
        {
            order = parsedOrder;
        }

        return new PreparedItem(sourceIndex, item, claimId, citations, buildText(item), order);
    }

    /// <summary>
    /// Second pass: builds the claims of a list section. <c>overrides</c> ordinals and the
    /// <c>sourceAnchor</c> both use the RAW source index; <c>displayOrder</c> is contiguous over
    /// emitted claims, following the declared <c>order</c> when <paramref name="sortByDeclaredOrder"/>.
    /// </summary>
    private static IEnumerable<MechanicClaim> EmitItems(
        Guid analysisId,
        MechanicSection section,
        string jsonProperty,
        IReadOnlyList<PreparedItem?> prepared,
        bool sortByDeclaredOrder)
    {
        var sectionClaimIds = prepared.Select(p => p?.ClaimId ?? Guid.Empty).ToList();

        IEnumerable<PreparedItem> sequence = prepared.OfType<PreparedItem>();
        if (sortByDeclaredOrder)
        {
            sequence = sequence.OrderBy(x => x.Order ?? int.MaxValue).ThenBy(x => x.SourceIndex);
        }

        var structures = SanitizeOverrideGraph(
            prepared.OfType<PreparedItem>()
                .ToDictionary(p => p.SourceIndex, p => ReadStructure(p.Element, sectionClaimIds, p.SourceIndex)),
            sectionClaimIds);

        var displayOrder = 0;
        foreach (var p in sequence)
        {
            yield return BuildClaim(
                claimId: p.ClaimId,
                analysisId: analysisId,
                section: section,
                text: p.Text,
                displayOrder: displayOrder++,
                citations: p.Citations,
                sourceAnchor: $"$.{jsonProperty}[{p.SourceIndex}]",
                structure: structures[p.SourceIndex]);
        }
    }

    /// <summary>
    /// Makes the persisted override graph of one section respect the spec §2 invariants the parser can
    /// enforce on its own (precedent: ruling R5). First every edge whose source OR target item is an
    /// <c>Example</c> is dropped (ruling R10). Then, each time a cycle is found (DFS in ascending raw-index
    /// order), the override edge leaving the cycle member with the HIGHEST raw source index is dropped, and
    /// the search repeats until no cycle remains. T5 reports both problems on the raw JSON
    /// (<c>T5_example_in_override</c>, <c>T5_override_cycle</c>); the parser only guarantees the persisted data.
    /// </summary>
    private static Dictionary<int, MechanicClaimStructure> SanitizeOverrideGraph(
        Dictionary<int, MechanicClaimStructure> structures, IReadOnlyList<Guid> sectionClaimIds)
    {
        var indexOf = new Dictionary<Guid, int>();
        for (var i = 0; i < sectionClaimIds.Count; i++)
        {
            if (sectionClaimIds[i] != Guid.Empty)
            {
                indexOf[sectionClaimIds[i]] = i;
            }
        }

        bool IsExample(int index) => structures[index].Kind == MechanicClaimKind.Example;

        var edges = structures.ToDictionary(
            kv => kv.Key, kv => kv.Value.Overrides.Select(g => indexOf[g]).ToList());

        var changed = new HashSet<int>();
        foreach (var (from, targets) in edges)
        {
            var before = targets.Count;
            if (IsExample(from))
            {
                targets.Clear();
            }
            else
            {
                targets.RemoveAll(IsExample);
            }

            if (targets.Count != before)
            {
                changed.Add(from);
            }
        }

        while (FindCycle(edges) is { } cycle)
        {
            var from = cycle.Max();
            var next = cycle[(cycle.IndexOf(from) + 1) % cycle.Count];
            edges[from].Remove(next);
            changed.Add(from);
        }

        foreach (var idx in changed)
        {
            structures[idx] = structures[idx] with { Overrides = edges[idx].Select(i => sectionClaimIds[i]).ToList() };
        }

        return structures;
    }

    /// <summary>Returns the nodes of one cycle in path order (each overrides the next, last overrides first), or null.</summary>
    private static List<int>? FindCycle(Dictionary<int, List<int>> edges)
    {
        var state = new Dictionary<int, int>(); // 1 = on stack, 2 = done
        var stack = new List<int>();

        List<int>? Visit(int node)
        {
            state[node] = 1;
            stack.Add(node);
            foreach (var next in edges[node])
            {
                var s = state.GetValueOrDefault(next);
                if (s == 1)
                {
                    return stack.Skip(stack.IndexOf(next)).ToList();
                }

                if (s == 0 && Visit(next) is { } found)
                {
                    return found;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
            return null;
        }

        foreach (var node in edges.Keys.OrderBy(x => x))
        {
            if (state.GetValueOrDefault(node) == 0 && Visit(node) is { } cycle)
            {
                return cycle;
            }
        }

        return null;
    }

    private static string LabelledText(JsonElement item, string labelProperty, string body)
    {
        var label = ReadString(item, labelProperty);
        return string.IsNullOrWhiteSpace(label)
            ? body
            : $"{label.Trim()}: {body.Trim()}";
    }

    /// <summary>
    /// Extracts <c>citations[]</c> under <paramref name="parent"/> and converts each entry to a
    /// <see cref="MechanicCitation"/> with the supplied <paramref name="claimId"/>. Drops entries
    /// that violate the citation factory's invariants so one bad quote doesn't take down an
    /// otherwise-valid claim.
    /// </summary>
    private static IEnumerable<MechanicCitation> ExtractCitations(JsonElement parent, Guid claimId)
    {
        if (!parent.TryGetProperty("citations", out var citations)
            || citations.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var displayOrder = 0;
        foreach (var entry in citations.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!entry.TryGetProperty("pdf_page", out var pageEl)
                || pageEl.ValueKind != JsonValueKind.Number
                || !pageEl.TryGetInt32(out var pdfPage)
                || pdfPage <= 0)
            {
                continue;
            }

            var quote = ReadString(entry, "quote");
            if (string.IsNullOrWhiteSpace(quote))
            {
                continue;
            }

            var trimmed = quote!.Trim();
            if (trimmed.Length > MechanicCitation.MaxQuoteChars)
            {
                continue;
            }

            if (MechanicCitation.CountWords(trimmed) > MechanicCitation.MaxQuoteWords)
            {
                continue;
            }

            Guid? chunkId = null;
            if (entry.TryGetProperty("chunk_id", out var chunkEl)
                && chunkEl.ValueKind == JsonValueKind.String
                && Guid.TryParse(chunkEl.GetString(), out var parsedChunkId))
            {
                chunkId = parsedChunkId;
            }

            MechanicCitation citation;
            try
            {
                citation = MechanicCitation.Create(
                    claimId: claimId,
                    pdfPage: pdfPage,
                    quote: trimmed,
                    chunkId: chunkId,
                    displayOrder: displayOrder);
            }
            catch (ArgumentException)
            {
                // Factory rejected — skip this citation, keep trying the rest.
                continue;
            }

            displayOrder++;
            yield return citation;
        }
    }

    /// <summary>
    /// Assembles a brand-new <see cref="MechanicClaim"/> via <see cref="MechanicClaim.CreateWithId"/>
    /// with the pre-allocated <paramref name="claimId"/>. Using <c>CreateWithId</c> (not
    /// <c>Reconstitute</c>) preserves <c>IsNew = true</c> so the repository's reattachment logic
    /// emits INSERT for the claim — without that flag, EF would mark the detached claim as
    /// <c>Modified</c> and child citation INSERTs would fail FK against a non-existent parent row.
    /// </summary>
    private static MechanicClaim BuildClaim(
        Guid claimId,
        Guid analysisId,
        MechanicSection section,
        string text,
        int displayOrder,
        IReadOnlyList<MechanicCitation> citations,
        string sourceAnchor,
        MechanicClaimStructure structure)
    {
        return MechanicClaim.CreateWithId(
            id: claimId,
            analysisId: analysisId,
            section: section,
            text: text.Trim(),
            displayOrder: displayOrder,
            citations: citations,
            sourceAnchor: sourceAnchor,
            structure: structure);
    }

    private static readonly IReadOnlyDictionary<string, MechanicClaimKind> KindNames =
        new Dictionary<string, MechanicClaimKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["rule"] = MechanicClaimKind.Rule,
            ["exception"] = MechanicClaimKind.Exception,
            ["clarification"] = MechanicClaimKind.Clarification,
            ["example"] = MechanicClaimKind.Example
        };

    private static readonly IReadOnlyDictionary<string, MechanicRulePriority> PriorityNames =
        new Dictionary<string, MechanicRulePriority>(StringComparer.OrdinalIgnoreCase)
        {
            ["base"] = MechanicRulePriority.Base,
            ["expansion"] = MechanicRulePriority.Expansion,
            ["card"] = MechanicRulePriority.Card,
            ["scenario"] = MechanicRulePriority.Scenario
        };

    /// <summary>
    /// Reads kind/priority/overrides/trigger from one section item (prompt v1.2.0). Overrides are
    /// 0-based ordinals into the RAW array of the same section — the index the LLM returned, also
    /// used by the <c>sourceAnchor</c>. <paramref name="sectionClaimIds"/> has one slot per raw
    /// item, <see cref="Guid.Empty"/> for items that produced no claim. An ordinal that is out of
    /// range, equal to <paramref name="selfOrdinal"/> or points at a skipped item is dropped here
    /// (the T5 guardrail reports it). Unknown kind/priority fall back to Rule/Base.
    /// </summary>
    internal static MechanicClaimStructure ReadStructure(
        JsonElement item, IReadOnlyList<Guid> sectionClaimIds, int selfOrdinal)
    {
        var kind = ReadString(item, "kind") is { } k && KindNames.TryGetValue(k.Trim(), out var kk)
            ? kk : MechanicClaimKind.Rule;
        var priority = ReadString(item, "priority") is { } p && PriorityNames.TryGetValue(p.Trim(), out var pp)
            ? pp : MechanicRulePriority.Base;

        var overrides = new List<Guid>();
        if (item.TryGetProperty("overrides", out var ov) && ov.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ov.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var ord)
                    && ord >= 0 && ord < sectionClaimIds.Count && ord != selfOrdinal
                    && sectionClaimIds[ord] != Guid.Empty
                    && !overrides.Contains(sectionClaimIds[ord]))
                {
                    overrides.Add(sectionClaimIds[ord]);
                }
            }
        }

        MechanicTrigger? trigger = null;
        if (item.TryGetProperty("trigger", out var tr) && tr.ValueKind == JsonValueKind.Object)
        {
            trigger = MechanicTrigger.Create(ReadString(tr, "phase"), ReadString(tr, "action"), ReadString(tr, "component"));
        }

        return new MechanicClaimStructure(kind, priority, overrides, trigger);
    }

    private static string? ReadString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
