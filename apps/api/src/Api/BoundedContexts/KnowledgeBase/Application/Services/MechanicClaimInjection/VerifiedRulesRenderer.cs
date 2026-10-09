using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

/// <summary>A claim-derived citation to emit on the RAG citation channel (source=claim), keyed to its
/// inline <c>[Vk]</c> marker in the rendered block.</summary>
internal sealed record VerifiedRuleCitation(int Marker, Guid PdfId, int PdfPage, string Quote);

/// <summary>The assembled <c>[Verified Rules …]</c> prompt block plus the ordered claim citations.</summary>
internal sealed record VerifiedRulesBlock(string PromptText, IReadOnlyList<VerifiedRuleCitation> Citations)
{
    public bool IsEmpty => PromptText.Length == 0;

    public static VerifiedRulesBlock Empty { get; } = new(string.Empty, Array.Empty<VerifiedRuleCitation>());
}

/// <summary>Rendering options (spec 2026-10-08 §5). Default = legacy behaviour, byte-identical.</summary>
internal sealed record VerifiedRulesRenderOptions(bool V3Ordering, bool IncludeExamples = false)
{
    public static VerifiedRulesRenderOptions Default { get; } = new(false);
}

/// <summary>
/// Renders approved claims of a <see cref="PublishedMechanicCardDto"/> into the authoritative
/// <c>[Verified Rules — human-approved]</c> prompt block (spec §7.2). Pure, stateless.
/// Uses the reformulated <c>Claim</c> text in the body (never the verbatim <c>Quote</c>, §16 copyright);
/// the verbatim <c>Quote</c> travels only in the structured <see cref="VerifiedRuleCitation"/>.
/// </summary>
internal static class VerifiedRulesRenderer
{
    public const string Header = "[Verified Rules — human-approved]";

    public static VerifiedRulesBlock Render(
        PublishedMechanicCardDto card,
        IReadOnlyList<MechanicSection> sections,
        int maxClaimsPerSection = 8,
        VerifiedRulesRenderOptions? options = null)
    {
        if (card?.Sections is null || sections is null || sections.Count == 0 || maxClaimsPerSection <= 0)
        {
            return VerifiedRulesBlock.Empty;
        }

        var sb = new StringBuilder();
        var citations = new List<VerifiedRuleCitation>();
        var marker = 0;

        foreach (var section in sections)
        {
            var name = section.ToString();
            var dto = card.Sections.FirstOrDefault(s =>
                string.Equals(s.Section, name, StringComparison.OrdinalIgnoreCase));
            if (dto?.Claims is not { Count: > 0 })
            {
                continue;
            }

            var ordered = options is { V3Ordering: true } ? OrderV3(dto.Claims, options.IncludeExamples) : dto.Claims;
            var take = Math.Min(maxClaimsPerSection, ordered.Count);
            if (take == 0)
            {
                continue; // e.g. a section of only Examples with examples excluded: no header, no empty block
            }

            if (sb.Length == 0)
            {
                sb.Append(Header);
            }

            sb.Append('\n').Append("## ").Append(name);

            // Assign markers BEFORE writing so "(Eccezione a [Vk])" can reference a marker of the same section.
            var markerOf = new Dictionary<Guid, int>();
            for (var i = 0; i < take; i++)
            {
                markerOf[ordered[i].Id] = marker + i + 1;
            }

            for (var i = 0; i < take; i++)
            {
                var claim = ordered[i];
                marker++;

                sb.Append("\n[V").Append(marker).Append("] ").Append(claim.Claim);
                if (claim.Citations is { Count: > 0 })
                {
                    sb.Append(" [Page ").Append(claim.Citations[0].PdfPage).Append(']');
                    foreach (var cite in claim.Citations)
                    {
                        citations.Add(new VerifiedRuleCitation(marker, cite.PdfId, cite.PdfPage, cite.Quote));
                    }
                }

                if (options is { V3Ordering: true })
                {
                    AppendV3Annotations(sb, claim, markerOf);
                }
            }
        }

        return sb.Length == 0 ? VerifiedRulesBlock.Empty : new VerifiedRulesBlock(sb.ToString(), citations);
    }

    private static void AppendV3Annotations(StringBuilder sb, PublishedMechanicCardClaimDto claim, Dictionary<Guid, int> markerOf)
    {
        if (claim.Kind == MechanicClaimKind.Exception && claim.Overrides is { Count: > 0 })
        {
            var refs = claim.Overrides.Where(markerOf.ContainsKey).Select(o => $"[V{markerOf[o]}]").ToList();
            if (refs.Count > 0)
            {
                sb.Append(" (Eccezione a ").Append(string.Join(", ", refs)).Append(')');
            }
        }

        if (claim.Trigger is { } t)
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(t.Phase) ? null : $"fase {t.Phase}",
                string.IsNullOrWhiteSpace(t.Action) ? null : $"azione {t.Action}",
                string.IsNullOrWhiteSpace(t.Component) ? null : $"componente {t.Component}"
            }.Where(p => p is not null).ToList();
            if (parts.Count > 0)
            {
                sb.Append(" (quando: ").Append(string.Join(" / ", parts)).Append(')');
            }
        }
    }

    /// <summary>Priority desc, then overriding-before-overridden (longest override chain first), then original order. Examples last or dropped.</summary>
    private static List<PublishedMechanicCardClaimDto> OrderV3(IReadOnlyList<PublishedMechanicCardClaimDto> claims, bool includeExamples)
    {
        var byId = claims.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var index = new Dictionary<Guid, int>();
        for (var i = 0; i < claims.Count; i++)
        {
            index.TryAdd(claims[i].Id, i);
        }

        var examples = claims.Where(c => c.Kind == MechanicClaimKind.Example).ToList();
        var rules = claims.Where(c => c.Kind != MechanicClaimKind.Example).ToList();

        // rank = longest override chain length ending at this claim (A<-B<-C => C rank 2, B 1, A 0); higher renders first
        var rank = new Dictionary<Guid, int>();
        int Rank(Guid id, HashSet<Guid> visiting)
        {
            if (rank.TryGetValue(id, out var r))
            {
                return r;
            }

            if (!visiting.Add(id))
            {
                return 0; // cycle guard: the domain forbids cycles, be defensive anyway
            }

            var best = 0;
            foreach (var o in byId[id].Overrides ?? Array.Empty<Guid>())
            {
                if (byId.ContainsKey(o))
                {
                    best = Math.Max(best, Rank(o, visiting) + 1);
                }
            }

            visiting.Remove(id);
            return rank[id] = best;
        }

        var ordered = rules
            .OrderByDescending(c => (int)c.Priority)
            .ThenByDescending(c => Rank(c.Id, new HashSet<Guid>()))
            .ThenBy(c => index[c.Id])
            .ToList();
        if (includeExamples)
        {
            ordered.AddRange(examples.OrderBy(c => index[c.Id]));
        }

        return ordered;
    }
}
