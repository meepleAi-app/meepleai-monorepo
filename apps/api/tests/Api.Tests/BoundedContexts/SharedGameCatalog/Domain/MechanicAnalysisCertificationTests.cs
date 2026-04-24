using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicAnalysisCertificationTests
{
    [Fact]
    public void ApplyMetricsResult_updates_status_and_lastMetricsId()
    {
        var a = MechanicAnalysisTestFactory.NewCompleted();
        var m = MechanicAnalysisMetricsTestFactory.NewCertified(a.Id);
        a.ApplyMetricsResult(m);

        a.CertificationStatus.Should().Be(CertificationStatus.Certified);
        a.LastMetricsId.Should().Be(m.Id);
        a.CertifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void CertifyViaOverride_rejects_empty_reason()
    {
        var a = MechanicAnalysisTestFactory.NewNotCertified();
        var act = () => a.CertifyViaOverride(reason: "", userId: Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CertifyViaOverride_requires_LastMetricsId()
    {
        var a = MechanicAnalysisTestFactory.NewCompleted(); // no metrics yet
        var act = () => a.CertifyViaOverride(reason: "Manual approval after human review of rulebook.", userId: Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CertifyViaOverride_rejects_when_already_certified()
    {
        var a = MechanicAnalysisTestFactory.NewCertified();
        var act = () => a.CertifyViaOverride(reason: "Manual approval after human review of rulebook.", userId: Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}

// ============================================================
// Test factories — scoped to this test assembly
// ============================================================

internal static class MechanicAnalysisTestFactory
{
    /// <summary>
    /// Builds a MechanicAnalysis that has been Published (all claims approved) but has no metrics
    /// yet — <see cref="MechanicAnalysis.LastMetricsId"/> is null and
    /// <see cref="MechanicAnalysis.CertificationStatus"/> is NotEvaluated.
    /// </summary>
    public static MechanicAnalysis NewCompleted()
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
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
                MechanicCitation.Create(
                    claimId: analysis.Id,
                    pdfPage: 1,
                    quote: "Each turn draw one card.",
                    chunkId: null,
                    displayOrder: 0)
            });

        analysis.AddClaim(claim);

        var reviewerId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        analysis.SubmitForReview(reviewerId, utcNow);
        analysis.ApproveClaim(claim.Id, reviewerId, utcNow);
        analysis.Approve(reviewerId, utcNow);

        return analysis;
    }

    /// <summary>
    /// Builds a Published MechanicAnalysis that has had NotCertified metrics applied —
    /// <see cref="MechanicAnalysis.LastMetricsId"/> is set but
    /// <see cref="MechanicAnalysis.CertificationStatus"/> is NotCertified.
    /// </summary>
    public static MechanicAnalysis NewNotCertified()
    {
        var analysis = NewCompleted();
        var metrics = MechanicAnalysisMetricsTestFactory.NewNotCertified(analysis.Id);
        analysis.ApplyMetricsResult(metrics);
        return analysis;
    }

    /// <summary>
    /// Builds a Published MechanicAnalysis that has had Certified metrics applied —
    /// <see cref="MechanicAnalysis.LastMetricsId"/> is set and
    /// <see cref="MechanicAnalysis.CertificationStatus"/> is Certified.
    /// </summary>
    public static MechanicAnalysis NewCertified()
    {
        var analysis = NewCompleted();
        var metrics = MechanicAnalysisMetricsTestFactory.NewCertified(analysis.Id);
        analysis.ApplyMetricsResult(metrics);
        return analysis;
    }
}

internal static class MechanicAnalysisMetricsTestFactory
{
    private static readonly string GoldenHash = new string('a', 64);

    /// <summary>Creates metrics that satisfy default certification thresholds (all scores ≥ thresholds).</summary>
    public static MechanicAnalysisMetrics NewCertified(Guid analysisId) =>
        MechanicAnalysisMetrics.Create(
            analysisId: analysisId,
            sharedGameId: Guid.NewGuid(),
            coveragePct: 90,
            pageAccuracyPct: 95,
            bggMatchPct: 90,
            thresholds: CertificationThresholds.Default(),
            goldenVersionHash: GoldenHash,
            matchDetailsJson: "{}");

    /// <summary>Creates metrics that fail default certification thresholds (coverage below minimum).</summary>
    public static MechanicAnalysisMetrics NewNotCertified(Guid analysisId) =>
        MechanicAnalysisMetrics.Create(
            analysisId: analysisId,
            sharedGameId: Guid.NewGuid(),
            coveragePct: 50,
            pageAccuracyPct: 50,
            bggMatchPct: 50,
            thresholds: CertificationThresholds.Default(),
            goldenVersionHash: GoldenHash,
            matchDetailsJson: "{}");
}
