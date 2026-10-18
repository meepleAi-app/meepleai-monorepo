using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicClaimStructureTests
{
    [Fact]
    public void Create_DefaultsToBaseRule()
    {
        var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
        var c = MechanicClaim.Create(Guid.NewGuid(), MechanicSection.Mechanics, "t", 0, new[] { cit });
        c.Kind.Should().Be(MechanicClaimKind.Rule);
        c.Priority.Should().Be(MechanicRulePriority.Base);
        c.Overrides.Should().BeEmpty();
        c.Trigger.Should().BeNull();
    }

    [Fact]
    public void Reconstitute_RoundTripsStructure()
    {
        var target = Guid.NewGuid();
        var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
        var c = MechanicClaim.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), MechanicSection.Phases, "t", 0, MechanicClaimStatus.Approved,
            null, null, null, new[] { cit },
            kind: MechanicClaimKind.Exception, priority: MechanicRulePriority.Scenario,
            overrides: new[] { target }, trigger: new MechanicTrigger("fase azione", null, null));
        c.Kind.Should().Be(MechanicClaimKind.Exception);
        c.Priority.Should().Be(MechanicRulePriority.Scenario);
        c.Overrides.Should().Equal(target);
        c.Trigger!.Phase.Should().Be("fase azione");
        c.IsNew.Should().BeFalse();
    }
}
