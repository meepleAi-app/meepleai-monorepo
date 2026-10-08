using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
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

    private static readonly MechanicClaimStructure ExampleStructure =
        new(MechanicClaimKind.Example, MechanicRulePriority.Base, Array.Empty<Guid>(), null);

    [Fact]
    public void SetClaimStructure_RejectsExampleThatOverrides()
    {
        var a = Analysis(2);
        var (ex, other) = (a.Claims[0], a.Claims[1]);
        var act = () => a.SetClaimStructure(ex.Id, ExampleStructure with { Overrides = new[] { other.Id } });
        act.Should().Throw<ArgumentException>()
            .WithMessage("An Example claim cannot override other claims. (Parameter 'structure')");
        ex.Kind.Should().Be(MechanicClaimKind.Rule, "nothing applied");
    }

    [Fact]
    public void SetClaimStructure_RejectsExampleWhenAlreadyOverridden()
    {
        var a = Analysis(2);
        var (target, exc) = (a.Claims[0], a.Claims[1]);
        a.SetClaimStructure(exc.Id, Exc(target.Id));
        var act = () => a.SetClaimStructure(target.Id, ExampleStructure);
        act.Should().Throw<ArgumentException>()
            .WithMessage("An Example claim cannot be the target of an override. (Parameter 'structure')");
        target.Kind.Should().Be(MechanicClaimKind.Rule, "nothing applied");
    }

    // Parser-built claims (CreateWithId) skip SetClaimStructure: an Exception whose only edge points at an Example.
    private static (MechanicAnalysis Analysis, MechanicClaim Rule, MechanicClaim Example, MechanicClaim Exception) InReviewWithExampleEdge()
    {
        var a = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(), pdfDocumentId: Guid.NewGuid(), promptVersion: "v1.2.0",
            createdBy: Guid.NewGuid(), createdAt: DateTime.UtcNow, modelUsed: "m", provider: "p", costCapUsd: 1m);
        MechanicClaim Build(int order, MechanicClaimStructure structure, Guid? id = null)
        {
            var claimId = id ?? Guid.NewGuid();
            var cit = MechanicCitation.Create(claimId, pdfPage: 1, quote: "q", chunkId: null, displayOrder: 0);
            return MechanicClaim.CreateWithId(claimId, a.Id, MechanicSection.Mechanics, $"claim {order}", order, new[] { cit },
                sourceAnchor: $"$.mechanics[{order}]", structure: structure);
        }

        var rule = Build(0, MechanicClaimStructure.Default);
        var example = Build(1, ExampleStructure);
        var exception = Build(2, Exc(example.Id));
        a.AddClaim(rule);
        a.AddClaim(example);
        a.AddClaim(exception);
        a.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        return (a, rule, example, exception);
    }

    [Fact]
    public void ApproveClaim_WithInvalidCurrentStructure_Throws()
    {
        var (a, rule, _, exc) = InReviewWithExampleEdge();

        var act = () => a.ApproveClaim(exc.Id, Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithMessage("An Example claim cannot be overridden. (Parameter 'structure')");
        exc.Status.Should().Be(MechanicClaimStatus.Pending, "validation runs before the approval");

        a.ApproveClaim(rule.Id, Guid.NewGuid(), DateTime.UtcNow);
        rule.Status.Should().Be(MechanicClaimStatus.Approved, "a valid sibling is unaffected");
    }

    [Fact]
    public void ApproveClaim_WithSuppliedStructure_ValidatesThatInsteadOfTheCurrentOne()
    {
        // The approve dialog sends the reviewer-corrected structure: it is the one that will be in effect.
        var (a, rule, _, exc) = InReviewWithExampleEdge();

        a.ApproveClaim(exc.Id, Guid.NewGuid(), DateTime.UtcNow, note: null, structure: Exc(rule.Id));

        exc.Status.Should().Be(MechanicClaimStatus.Approved);
        exc.Overrides.Should().Equal(rule.Id);
    }

    [Fact]
    public void ApproveClaim_WithInvalidSuppliedStructure_ThrowsAndLeavesClaimUntouched()
    {
        var (a, _, example, exc) = InReviewWithExampleEdge();
        var before = exc.Overrides.ToList();

        var act = () => a.ApproveClaim(exc.Id, Guid.NewGuid(), DateTime.UtcNow, note: null, structure: Exc(exc.Id));

        act.Should().Throw<ArgumentException>().WithMessage("A claim cannot override itself. (Parameter 'structure')");
        exc.Status.Should().Be(MechanicClaimStatus.Pending);
        exc.Overrides.Should().Equal(before);
        example.Kind.Should().Be(MechanicClaimKind.Example);
    }

    [Fact]
    public void Approve_WithInvalidClaimGraph_Throws409()
    {
        // Claims approved before the gate existed (entity-level Approve bypasses the aggregate check):
        // the publish transition is the last line of defence and maps to 409 in the handler.
        var (a, _, _, exc) = InReviewWithExampleEdge();
        foreach (var claim in a.Claims)
        {
            claim.Approve(Guid.NewGuid(), DateTime.UtcNow);
        }

        var act = () => a.Approve(Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<InvalidMechanicAnalysisStateException>()
            .WithMessage($"*claim {a.Claims[1].Id}*invalid structure*");
        a.Status.Should().Be(MechanicAnalysisStatus.InReview);
        exc.Status.Should().Be(MechanicClaimStatus.Approved);
    }

    [Fact]
    public void HasValidClaimStructure_ReportsBothEndsOfAnExampleEdge()
    {
        var (a, rule, example, exc) = InReviewWithExampleEdge();

        a.HasValidClaimStructure(rule.Id).Should().BeTrue();
        a.HasValidClaimStructure(example.Id).Should().BeFalse("an Example cannot be the target of an override");
        a.HasValidClaimStructure(exc.Id).Should().BeFalse("an Example cannot be overridden");
    }
}
