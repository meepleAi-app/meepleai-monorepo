using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Parses the six section-level JSON envelopes emitted by the Mechanic Extractor pipeline
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
/// via <see cref="MechanicClaim.Reconstitute"/> with the pre-allocated Id, and enforce the
/// non-blank text and ≥1 citation invariants here in the parser.</description></item>
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
            citations: citations);
    }

    // ============================================================
    // Section: Mechanics
    // Schema: { "mechanics": [{ "name": "...", "description": "...", "citations": [...] }] }
    // ============================================================
    private static IEnumerable<MechanicClaim> ParseMechanics(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("mechanics", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var displayOrder = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var description = ReadString(item, "description");
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            var claimId = Guid.NewGuid();
            var citations = ExtractCitations(item, claimId).ToList();
            if (citations.Count == 0)
            {
                continue;
            }

            var name = ReadString(item, "name");
            var text = string.IsNullOrWhiteSpace(name)
                ? description!
                : $"{name!.Trim()}: {description!.Trim()}";

            yield return BuildClaim(
                claimId: claimId,
                analysisId: analysisId,
                section: MechanicSection.Mechanics,
                text: text,
                displayOrder: displayOrder++,
                citations: citations);
        }
    }

    // ============================================================
    // Section: Victory
    // Schema: { "victory": { "primary": "...", "alternatives": ["..."], "citations": [...] } }
    // The envelope holds one citation array shared between primary and alternatives.
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

        yield return BuildClaim(
            claimId: primaryClaimId,
            analysisId: analysisId,
            section: MechanicSection.Victory,
            text: primary!,
            displayOrder: displayOrder++,
            citations: primaryCitations);

        // Alternatives reuse the same citation source — re-extract per claim so ClaimId wires up.
        if (!victory.TryGetProperty("alternatives", out var alternatives)
            || alternatives.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var alt in alternatives.EnumerateArray())
        {
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
                citations: altCitations);
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
            yield break;
        }

        var displayOrder = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var usage = ReadString(item, "usage");
            if (string.IsNullOrWhiteSpace(usage))
            {
                continue;
            }

            var claimId = Guid.NewGuid();
            var citations = ExtractCitations(item, claimId).ToList();
            if (citations.Count == 0)
            {
                continue;
            }

            var name = ReadString(item, "name");
            var text = string.IsNullOrWhiteSpace(name)
                ? usage!
                : $"{name!.Trim()}: {usage!.Trim()}";

            yield return BuildClaim(
                claimId: claimId,
                analysisId: analysisId,
                section: MechanicSection.Resources,
                text: text,
                displayOrder: displayOrder++,
                citations: citations);
        }
    }

    // ============================================================
    // Section: Phases
    // Schema: { "phases": [{ "name": "...", "description": "...", "order": 1, "citations": [...] }] }
    // Entries are emitted in their declared `order`; when missing or duplicate we fall back to
    // the source array order.
    // ============================================================
    private static IEnumerable<MechanicClaim> ParsePhases(Guid analysisId, JsonElement root)
    {
        if (!root.TryGetProperty("phases", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var buffered = new List<(int? Order, int SourceIndex, JsonElement Element)>();
        var sourceIndex = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                sourceIndex++;
                continue;
            }

            int? order = null;
            if (item.TryGetProperty("order", out var orderEl)
                && orderEl.ValueKind == JsonValueKind.Number
                && orderEl.TryGetInt32(out var parsedOrder))
            {
                order = parsedOrder;
            }

            buffered.Add((order, sourceIndex, item));
            sourceIndex++;
        }

        var ordered = buffered
            .OrderBy(x => x.Order ?? int.MaxValue)
            .ThenBy(x => x.SourceIndex);

        var displayOrder = 0;
        foreach (var (_, _, item) in ordered)
        {
            var description = ReadString(item, "description");
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            var claimId = Guid.NewGuid();
            var citations = ExtractCitations(item, claimId).ToList();
            if (citations.Count == 0)
            {
                continue;
            }

            var name = ReadString(item, "name");
            var text = string.IsNullOrWhiteSpace(name)
                ? description!
                : $"{name!.Trim()}: {description!.Trim()}";

            yield return BuildClaim(
                claimId: claimId,
                analysisId: analysisId,
                section: MechanicSection.Phases,
                text: text,
                displayOrder: displayOrder++,
                citations: citations);
        }
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
            yield break;
        }

        var displayOrder = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var answer = ReadString(item, "answer");
            if (string.IsNullOrWhiteSpace(answer))
            {
                continue;
            }

            var claimId = Guid.NewGuid();
            var citations = ExtractCitations(item, claimId).ToList();
            if (citations.Count == 0)
            {
                continue;
            }

            var question = ReadString(item, "question");
            var text = string.IsNullOrWhiteSpace(question)
                ? answer!
                : $"Q: {question!.Trim()}\nA: {answer!.Trim()}";

            yield return BuildClaim(
                claimId: claimId,
                analysisId: analysisId,
                section: MechanicSection.Faq,
                text: text,
                displayOrder: displayOrder++,
                citations: citations);
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

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
    /// Assembles a <see cref="MechanicClaim"/> via <see cref="MechanicClaim.Reconstitute"/> with
    /// the pre-allocated <paramref name="claimId"/>. Reconstitute is used (not <c>Create</c>) so
    /// citation FKs resolve at persistence time; text/displayOrder invariants are enforced by the
    /// caller's trimming + the non-empty text guard above.
    /// </summary>
    private static MechanicClaim BuildClaim(
        Guid claimId,
        Guid analysisId,
        MechanicSection section,
        string text,
        int displayOrder,
        IReadOnlyList<MechanicCitation> citations)
    {
        return MechanicClaim.Reconstitute(
            id: claimId,
            analysisId: analysisId,
            section: section,
            text: text.Trim(),
            displayOrder: displayOrder,
            status: MechanicClaimStatus.Pending,
            reviewedBy: null,
            reviewedAt: null,
            rejectionNote: null,
            citations: citations);
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
