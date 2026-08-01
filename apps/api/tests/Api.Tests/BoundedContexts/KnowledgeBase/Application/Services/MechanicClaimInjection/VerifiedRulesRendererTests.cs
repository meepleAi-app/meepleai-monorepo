using System;
using System.Collections.Generic;
using System.Linq;
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class VerifiedRulesRendererTests
{
    private static readonly Guid Pdf = Guid.NewGuid();

    private static PublishedMechanicCardCitationDto Cite(int page, string quote) => new(Pdf, page, quote);

    private static PublishedMechanicCardClaimDto Claim(string text, params PublishedMechanicCardCitationDto[] cites)
        => new(Guid.NewGuid(), text, cites);

    private static PublishedMechanicCardDto Card(params PublishedMechanicCardSectionDto[] sections)
        => new(
            CardId: Guid.NewGuid(),
            SharedGameId: Guid.NewGuid(),
            Title: "Test Card",
            Version: 1,
            PublishedAt: DateTime.UtcNow,
            GameName: "Test Game",
            Publisher: "Pub",
            Language: "it",
            Sections: sections,
            SourceAnalysisId: Guid.NewGuid(),
            PublicationYear: 2020,
            DocumentName: "rulebook.pdf");

    private static PublishedMechanicCardDto SampleCard() => Card(
        new PublishedMechanicCardSectionDto(nameof(MechanicSection.Setup), new[]
        {
            Claim("I giocatori ricevono 2 carte progetto.", Cite(3, "each player draws two project cards")),
            Claim("In una partita a 3 si usa la plancia standard.", Cite(3, "3-player uses the standard board")),
        }),
        new PublishedMechanicCardSectionDto(nameof(MechanicSection.Components), new[]
        {
            Claim("Ogni giocatore prende 40 cubi.", Cite(2, "forty cubes per player")),
        }),
        new PublishedMechanicCardSectionDto(nameof(MechanicSection.Victory), new[]
        {
            Claim("Vince chi ha piu' punti.", Cite(9, "most points wins")),
        }));

    [Fact]
    public void Render_IncludesHeaderSectionHeadersAndNumberedClaimsForRequestedSectionsOnly()
    {
        var block = VerifiedRulesRenderer.Render(
            SampleCard(),
            new[] { MechanicSection.Setup, MechanicSection.Components });

        block.PromptText.Should().StartWith(VerifiedRulesRenderer.Header);
        block.PromptText.Should().Contain("## Setup");
        block.PromptText.Should().Contain("## Components");
        block.PromptText.Should().Contain("[V1]");
        block.PromptText.Should().Contain("[V2]");
        block.PromptText.Should().Contain("[V3]");
        block.PromptText.Should().Contain("[Page 3]");
        block.PromptText.Should().Contain("[Page 2]");
        // Victory was NOT requested → excluded
        block.PromptText.Should().NotContain("## Victory");
    }

    [Fact]
    public void Render_UsesReformulatedClaimText_NotVerbatimQuote()
    {
        var block = VerifiedRulesRenderer.Render(SampleCard(), new[] { MechanicSection.Setup });

        block.PromptText.Should().Contain("I giocatori ricevono 2 carte progetto.");
        // Copyright rule (§7.2/§16): verbatim Quote must NOT appear in the prompt body.
        block.PromptText.Should().NotContain("each player draws two project cards");
    }

    [Fact]
    public void Render_EmitsStructuredCitationsMappedToMarkers()
    {
        var block = VerifiedRulesRenderer.Render(
            SampleCard(),
            new[] { MechanicSection.Setup, MechanicSection.Components });

        block.Citations.Should().HaveCount(3); // 2 Setup + 1 Components claims, one cite each
        block.Citations.Select(c => c.Marker).Should().Equal(1, 2, 3);
        block.Citations.Should().OnlyContain(c => c.PdfId == Pdf);
        block.Citations[0].Quote.Should().Be("each player draws two project cards");
        block.Citations[0].PdfPage.Should().Be(3);
        block.Citations[2].PdfPage.Should().Be(2);
    }

    [Fact]
    public void Render_RespectsPerSectionCap()
    {
        var block = VerifiedRulesRenderer.Render(
            SampleCard(),
            new[] { MechanicSection.Setup },
            maxClaimsPerSection: 1);

        block.PromptText.Should().Contain("[V1]");
        block.PromptText.Should().NotContain("[V2]");
        block.Citations.Should().HaveCount(1);
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenNoRequestedSectionPresentInCard()
    {
        var block = VerifiedRulesRenderer.Render(SampleCard(), new[] { MechanicSection.Faq });

        block.PromptText.Should().BeEmpty();
        block.Citations.Should().BeEmpty();
    }

    [Fact]
    public void Render_UsesGlobalContiguousMarkerNumberingAcrossSections()
    {
        var block = VerifiedRulesRenderer.Render(
            SampleCard(),
            new[] { MechanicSection.Setup, MechanicSection.Components });

        // Setup claims → [V1],[V2]; Components claim → [V3] (contiguous across the section boundary)
        var idxV2 = block.PromptText.IndexOf("[V2]", StringComparison.Ordinal);
        var idxComponents = block.PromptText.IndexOf("## Components", StringComparison.Ordinal);
        var idxV3 = block.PromptText.IndexOf("[V3]", StringComparison.Ordinal);
        idxV2.Should().BeLessThan(idxComponents);
        idxComponents.Should().BeLessThan(idxV3);
    }
}
