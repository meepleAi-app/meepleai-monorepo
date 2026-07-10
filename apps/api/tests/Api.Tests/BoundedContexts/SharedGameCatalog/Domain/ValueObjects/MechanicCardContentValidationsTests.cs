using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="MechanicCardContent.FromAnalysis"/> down-projection of
/// <see cref="MechanicClaim.Validations"/> into <see cref="MechanicCardValidationSnapshot"/> (#2782
/// FU-1 D6). The card is a published snapshot of accepted claims, so both "fail" AND "notRun"
/// outcomes collapse to <c>Passed = false</c> — only "pass" projects to <c>true</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardContentValidationsTests
{
    private static readonly DateTime Now = new(2026, 07, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FromAnalysis_DownProjectsClaimValidations_AndBumpsSchemaVersion()
    {
        var analysis = BuildPublishedAnalysisWithValidatedClaim();
        var ctx = BuildGameContext(analysis.SharedGameId);

        var content = MechanicCardContent.FromAnalysis(analysis, ctx, Now);

        content.SchemaVersion.Should().Be(2);
        var claim = content.Claims.Single();
        claim.Validations.Single(v => v.Rule == "T2").Passed.Should().BeFalse();
        claim.Validations.Single(v => v.Rule == "T3b").Passed.Should().BeTrue();
        claim.Validations.Single(v => v.Rule == "T3b").Score.Should().Be(0.9);
    }

    [Fact]
    public void FromAnalysis_DownProjectsNotRunOutcome_AsPassedFalse()
    {
        var analysis = BuildPublishedAnalysisWithValidatedClaim();
        var ctx = BuildGameContext(analysis.SharedGameId);

        var content = MechanicCardContent.FromAnalysis(analysis, ctx, Now);

        var claim = content.Claims.Single();
        var notRun = claim.Validations.Single(v => v.Rule == "T4");
        notRun.Passed.Should().BeFalse();
        notRun.Score.Should().BeNull();
    }

    [Fact]
    public void FromAnalysis_WithNoValidationsAttached_ProjectsEmptyArray()
    {
        var analysis = PublishedAnalysisWithClaim(attachValidations: false);
        var ctx = BuildGameContext(analysis.SharedGameId);

        var content = MechanicCardContent.FromAnalysis(analysis, ctx, Now);

        content.Claims.Single().Validations.Should().BeEmpty();
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static MechanicCardGameContext BuildGameContext(Guid sharedGameId) => new()
    {
        SharedGameId = sharedGameId,
        SharedGameName = "Catan",
        Publisher = "Kosmos",
        Language = "en"
    };

    private static MechanicAnalysis BuildPublishedAnalysisWithValidatedClaim() =>
        PublishedAnalysisWithClaim(attachValidations: true);

    private static MechanicAnalysis PublishedAnalysisWithClaim(bool attachValidations)
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: Now,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        var claim = MechanicClaim.Create(
            analysisId: analysis.Id,
            section: MechanicSection.Mechanics,
            text: "Draw phase",
            displayOrder: 0,
            citations: new[]
            {
                MechanicCitation.Create(analysis.Id, pdfPage: 1, quote: "Each turn draw one card.", chunkId: null, displayOrder: 0)
            });

        if (attachValidations)
        {
            claim.AttachValidations(new[]
            {
                new MechanicClaimValidation("T1", "pass", null, null),
                new MechanicClaimValidation("T2", "fail", "long verbatim", null),
                new MechanicClaimValidation("T3a", "pass", null, null),
                new MechanicClaimValidation("T3b", "pass", null, 0.9),
                new MechanicClaimValidation("T4", "notRun", null, null)
            });
        }

        analysis.AddClaim(claim);

        var reviewer = Guid.NewGuid();
        analysis.SubmitForReview(reviewer, Now);
        analysis.ApproveClaim(analysis.Claims[0].Id, reviewer, Now);
        analysis.Approve(reviewer, Now);

        return analysis;
    }
}
