using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisClaimStructureTests
{
    private static MechanicAnalysis Analysis(int claims)
    {
        var a = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(), pdfDocumentId: Guid.NewGuid(), promptVersion: "v1.2.0",
            createdBy: Guid.NewGuid(), createdAt: DateTime.UtcNow, modelUsed: "m", provider: "p", costCapUsd: 1m);
        for (var i = 0; i < claims; i++)
        {
            var cit = MechanicCitation.Create(Guid.NewGuid(), pdfPage: 1, quote: "q", chunkId: null, displayOrder: 0);
            a.AddClaim(MechanicClaim.Create(a.Id, MechanicSection.Mechanics, $"claim {i}", i, new[] { cit }));
        }
        return a;
    }

    private static MechanicClaimStructure Exc(params Guid[] overrides) =>
        new(MechanicClaimKind.Exception, MechanicRulePriority.Card, overrides, null);

    [Fact]
    public void SetClaimStructure_StoresFieldsOnClaim()
    {
        var a = Analysis(2);
        var (general, exc) = (a.Claims[0], a.Claims[1]);

        a.SetClaimStructure(exc.Id, Exc(general.Id));

        exc.Kind.Should().Be(MechanicClaimKind.Exception);
        exc.Priority.Should().Be(MechanicRulePriority.Card);
        exc.Overrides.Should().ContainSingle().Which.Should().Be(general.Id);
        exc.Trigger.Should().BeNull();
        general.Kind.Should().Be(MechanicClaimKind.Rule, "default untouched");
    }

    [Fact]
    public void SetClaimStructure_RejectsSelfOverride()
    {
        var a = Analysis(1);
        var c = a.Claims[0];
        var act = () => a.SetClaimStructure(c.Id, Exc(c.Id));
        act.Should().Throw<ArgumentException>().WithMessage("*itself*");
    }

    [Fact]
    public void SetClaimStructure_RejectsOverrideOutsideAnalysis()
    {
        var a = Analysis(1);
        var act = () => a.SetClaimStructure(a.Claims[0].Id, Exc(Guid.NewGuid()));
        act.Should().Throw<ArgumentException>().WithMessage("*same analysis*");
    }

    [Fact]
    public void SetClaimStructure_RejectsLongCycle()
    {
        var a = Analysis(3);
        var (x, y, z) = (a.Claims[0], a.Claims[1], a.Claims[2]);
        a.SetClaimStructure(x.Id, Exc(y.Id));
        a.SetClaimStructure(y.Id, Exc(z.Id));
        var act = () => a.SetClaimStructure(z.Id, Exc(x.Id));
        act.Should().Throw<ArgumentException>().WithMessage("*cycle*");
    }

    [Fact]
    public void SetClaimStructure_RejectsExampleInOverrideGraph()
    {
        var a = Analysis(2);
        var (ex, other) = (a.Claims[0], a.Claims[1]);
        a.SetClaimStructure(ex.Id, new MechanicClaimStructure(MechanicClaimKind.Example, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        var act = () => a.SetClaimStructure(other.Id, Exc(ex.Id));
        act.Should().Throw<ArgumentException>().WithMessage("*Example*");
    }

    [Fact]
    public void SetClaimStructure_RejectsUnboundException()
    {
        var a = Analysis(1);
        var act = () => a.SetClaimStructure(a.Claims[0].Id, new MechanicClaimStructure(MechanicClaimKind.Exception, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        act.Should().Throw<ArgumentException>().WithMessage("*Exception*Overrides*Trigger*");
    }

    [Fact]
    public void SetClaimStructure_AllowedOnApproved_RejectedOnRejected()
    {
        var a = Analysis(2);
        a.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        var reviewer = Guid.NewGuid();
        a.ApproveClaim(a.Claims[0].Id, reviewer, DateTime.UtcNow);
        a.RejectClaim(a.Claims[1].Id, reviewer, "no", DateTime.UtcNow);

        a.SetClaimStructure(a.Claims[0].Id, new MechanicClaimStructure(MechanicClaimKind.Clarification, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        a.Claims[0].Kind.Should().Be(MechanicClaimKind.Clarification);

        var act = () => a.SetClaimStructure(a.Claims[1].Id, MechanicClaimStructure.Default);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Rejected*");
    }
}
