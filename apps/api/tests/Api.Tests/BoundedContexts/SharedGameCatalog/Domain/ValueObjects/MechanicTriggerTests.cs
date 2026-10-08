using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicTriggerTests
{
    [Theory]
    [InlineData("  Fase di Azione ", "fase di azione")]
    [InlineData("CARTE", "carte")]
    public void Normalize_TrimsLowercasesAndCollapsesWhitespace(string raw, string expected)
    {
        MechanicTrigger.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void Create_ReturnsNull_WhenEveryFieldIsBlank()
    {
        MechanicTrigger.Create(" ", null, "").Should().BeNull();
    }

    [Fact]
    public void Create_NormalizesEachField()
    {
        var t = MechanicTrigger.Create("Fase  Azione", null, "Cavaliere ");
        t.Should().NotBeNull();
        t!.Phase.Should().Be("fase azione");
        t.Action.Should().BeNull();
        t.Component.Should().Be("cavaliere");
        t.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Structure_Default_IsBaseRuleWithoutOverridesOrTrigger()
    {
        var s = MechanicClaimStructure.Default;
        s.Kind.Should().Be(Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicClaimKind.Rule);
        s.Priority.Should().Be(Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicRulePriority.Base);
        s.Overrides.Should().BeEmpty();
        s.Trigger.Should().BeNull();
    }
}
