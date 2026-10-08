using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class VerifiedRulesRendererOrderingTests
{
    private static readonly Guid Pdf = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid(), E = Guid.NewGuid();

    private static PublishedMechanicCardClaimDto Claim(Guid id, string text, MechanicClaimKind kind, MechanicRulePriority prio, params Guid[] overrides)
        => new(id, text, new[] { new PublishedMechanicCardCitationDto(Pdf, 1, "q") }, kind, prio, overrides, null);

    private static PublishedMechanicCardDto Card(params PublishedMechanicCardClaimDto[] claims) => new(
        Guid.NewGuid(), Guid.NewGuid(), "T", 1, DateTime.UtcNow, "G", null, "it",
        new[] { new PublishedMechanicCardSectionDto(nameof(MechanicSection.Phases), claims) }, Guid.NewGuid(), null, null);

    // Ordine di arrivo: A (base) , B (card, overrides A), C (base), D (scenario, overrides B), E (example)
    private static PublishedMechanicCardDto Sample() => Card(
        Claim(A, "A generale", MechanicClaimKind.Rule, MechanicRulePriority.Base),
        Claim(B, "B eccezione", MechanicClaimKind.Exception, MechanicRulePriority.Card, A),
        Claim(C, "C generale", MechanicClaimKind.Rule, MechanicRulePriority.Base),
        Claim(D, "D scenario", MechanicClaimKind.Exception, MechanicRulePriority.Scenario, B),
        Claim(E, "E esempio", MechanicClaimKind.Example, MechanicRulePriority.Base));

    [Fact]
    public void FlagOff_OutputIsByteIdenticalToLegacy()
    {
        var legacy = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases });
        var withOptions = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 8, VerifiedRulesRenderOptions.Default);
        withOptions.PromptText.Should().Be(legacy.PromptText);
        legacy.PromptText.Should().Be("[Verified Rules — human-approved]\n## Phases\n[V1] A generale [Page 1]\n[V2] B eccezione [Page 1]\n[V3] C generale [Page 1]\n[V4] D scenario [Page 1]\n[V5] E esempio [Page 1]");
    }

    [Fact]
    public void FlagOn_OrdersByPriorityThenTopologically_ExcludesExamples_AndLabelsExceptions()
    {
        var block = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 8, new VerifiedRulesRenderOptions(V3Ordering: true));
        block.PromptText.Should().Be(
            "[Verified Rules — human-approved]\n## Phases\n" +
            "[V1] D scenario [Page 1] (Eccezione a [V2])\n" +
            "[V2] B eccezione [Page 1] (Eccezione a [V3])\n" +
            "[V3] A generale [Page 1]\n" +
            "[V4] C generale [Page 1]");
        block.Citations.Select(c => c.Marker).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void FlagOn_CapAppliesAfterOrdering()
    {
        var block = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 2, new VerifiedRulesRenderOptions(V3Ordering: true));
        block.PromptText.Should().Contain("[V1] D scenario").And.Contain("[V2] B eccezione").And.NotContain("A generale");
    }

    [Fact]
    public void FlagOn_LongChain_IsOrderedOverridingFirst()
    {
        var card = Card(
            Claim(A, "A", MechanicClaimKind.Rule, MechanicRulePriority.Base),
            Claim(B, "B", MechanicClaimKind.Exception, MechanicRulePriority.Base, A),
            Claim(C, "C", MechanicClaimKind.Exception, MechanicRulePriority.Base, B),
            Claim(D, "D", MechanicClaimKind.Exception, MechanicRulePriority.Base, C));
        var block = VerifiedRulesRenderer.Render(card, new[] { MechanicSection.Phases }, 8, new VerifiedRulesRenderOptions(V3Ordering: true));
        block.PromptText.Should().Match("*[V1] D*[V2] C*[V3] B*[V4] A*");
    }

    [Fact]
    public void FlagOn_IncludeExamples_RendersThemLast()
    {
        var block = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 8, new VerifiedRulesRenderOptions(V3Ordering: true, IncludeExamples: true));
        block.PromptText.Should().EndWith("[V5] E esempio [Page 1]");
    }
}
