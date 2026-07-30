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
        int maxClaimsPerSection = 8)
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

            if (sb.Length == 0)
            {
                sb.Append(Header);
            }

            sb.Append('\n').Append("## ").Append(name);

            var take = Math.Min(maxClaimsPerSection, dto.Claims.Count);
            for (var i = 0; i < take; i++)
            {
                var claim = dto.Claims[i];
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
            }
        }

        return sb.Length == 0 ? VerifiedRulesBlock.Empty : new VerifiedRulesBlock(sb.ToString(), citations);
    }
}
