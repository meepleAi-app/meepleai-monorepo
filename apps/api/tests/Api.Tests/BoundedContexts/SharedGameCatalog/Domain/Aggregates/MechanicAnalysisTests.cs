using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for the <see cref="MechanicAnalysis"/> aggregate root (Issue #523, M1.1).
/// Covers aggregate invariants (AC-1..AC-3), state machine transitions (AC-10, AC-12),
/// suppression kill-switch (T5), and cost cap override (T8).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisTests
{
    // ============================================================
    // Factory (Create)
    // ============================================================

    [Fact]
    public void Create_WithValidInputs_ProducesDraftWithDefaults()
    {
        var analysis = NewAnalysisWithClaim();

        analysis.Id.Should().NotBe(Guid.Empty);
        analysis.Status.Should().Be(MechanicAnalysisStatus.Draft);
        analysis.IsSuppressed.Should().BeFalse();
        analysis.TotalTokensUsed.Should().Be(0);
        analysis.EstimatedCostUsd.Should().Be(0m);
        analysis.ReviewedBy.Should().BeNull();
        analysis.ReviewedAt.Should().BeNull();
        analysis.RejectionReason.Should().BeNull();
        analysis.DomainEvents.Should().BeEmpty(); // Create() does not raise events.
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankPromptVersion_Throws(string promptVersion)
    {
        var act = () => MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: promptVersion,
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        act.Should().Throw<ArgumentException>().WithMessage("*PromptVersion*");
    }

    [Fact]
    public void Create_WithNonPositiveCostCap_Throws()
    {
        var act = () => MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "m",
            provider: "p",
            costCapUsd: 0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // AC-3: at least one citation per claim (enforced by MechanicClaim factory)
    // ============================================================

    [Fact]
    public void CreateClaim_WithoutCitations_Throws()
    {
        var analysisId = Guid.NewGuid();
        var act = () => MechanicClaim.Create(
            analysisId,
            MechanicSection.Mechanics,
            "A rule",
            displayOrder: 0,
            citations: Array.Empty<MechanicCitation>());

        act.Should().Throw<ArgumentException>().WithMessage("*citation*");
    }

    // ============================================================
    // AC-1: ≤25 word citation cap (T1)
    // ============================================================

    [Fact]
    public void CreateCitation_WithQuoteOver25Words_Throws()
    {
        var tooLong = string.Join(' ', Enumerable.Repeat("word", 26));
        var act = () => MechanicCitation.Create(
            Guid.NewGuid(),
            pdfPage: 1,
            quote: tooLong,
            chunkId: null,
            displayOrder: 0);

        act.Should().Throw<ArgumentException>().WithMessage("*25*");
    }

    [Fact]
    public void CreateCitation_WithQuoteExactly25Words_Succeeds()
    {
        var boundary = string.Join(' ', Enumerable.Repeat("word", 25));
        var citation = MechanicCitation.Create(
            Guid.NewGuid(),
            pdfPage: 3,
            quote: boundary,
            chunkId: null,
            displayOrder: 0);

        citation.Quote.Should().Be(boundary);
    }

    // ============================================================
    // State machine: SubmitForReview
    // ============================================================

    [Fact]
    public void SubmitForReview_FromDraft_TransitionsToInReview_AndRaisesEvent()
    {
        var analysis = NewAnalysisWithClaim();
        var actor = Guid.NewGuid();

        analysis.SubmitForReview(actor, DateTime.UtcNow);

        analysis.Status.Should().Be(MechanicAnalysisStatus.InReview);
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisStatusChangedEvent>();
    }

    [Fact]
    public void SubmitForReview_FromPublished_Throws()
    {
        var analysis = PublishedAnalysis(out _, out _);

        var act = () => analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<InvalidMechanicAnalysisStateException>();
    }

    [Fact]
    public void SubmitForReview_WithNoClaims_Throws()
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "m",
            provider: "p",
            costCapUsd: 1m);

        var act = () => analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>().WithMessage("*no claims*");
    }

    // ============================================================
    // AC-10: Approve requires ALL claims Approved
    // ============================================================

    [Fact]
    public void Approve_WithPendingClaim_Throws()
    {
        var analysis = NewAnalysisWithClaim();
        analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);

        var act = () => analysis.Approve(Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not all claims are Approved*");
    }

    [Fact]
    public void Approve_AfterAllClaimsApproved_TransitionsToPublished()
    {
        var analysis = PublishedAnalysis(out var reviewerId, out var utcNow);

        analysis.Status.Should().Be(MechanicAnalysisStatus.Published);
        analysis.ReviewedBy.Should().Be(reviewerId);
        analysis.ReviewedAt.Should().Be(utcNow);
        analysis.DomainEvents.OfType<MechanicAnalysisStatusChangedEvent>()
            .Should().ContainSingle(e => e.ToStatus == MechanicAnalysisStatus.Published);
    }

    // ============================================================
    // Reject + Resubmit path
    // ============================================================

    [Fact]
    public void Reject_FromInReview_TransitionsToRejected_WithReason()
    {
        var analysis = NewAnalysisWithClaim();
        analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        analysis.ClearDomainEvents();

        analysis.Reject(Guid.NewGuid(), "Missing evidence", DateTime.UtcNow);

        analysis.Status.Should().Be(MechanicAnalysisStatus.Rejected);
        analysis.RejectionReason.Should().Be("Missing evidence");
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisStatusChangedEvent>();
    }

    [Fact]
    public void SubmitForReview_AfterRejection_ResetsClaimsToPending_AndClearsRejectionReason()
    {
        var analysis = NewAnalysisWithClaim();
        var adminId = Guid.NewGuid();
        analysis.SubmitForReview(adminId, DateTime.UtcNow);
        analysis.ApproveClaim(analysis.Claims[0].Id, adminId, DateTime.UtcNow);
        analysis.Reject(adminId, "mistake", DateTime.UtcNow);

        analysis.Claims[0].Status.Should().Be(MechanicClaimStatus.Approved);
        analysis.RejectionReason.Should().Be("mistake");

        analysis.SubmitForReview(adminId, DateTime.UtcNow);

        analysis.Status.Should().Be(MechanicAnalysisStatus.InReview);
        analysis.Claims[0].Status.Should().Be(MechanicClaimStatus.Pending);
        analysis.RejectionReason.Should().BeNull();
    }

    // ============================================================
    // ApproveClaim / RejectClaim: only in InReview
    // ============================================================

    [Fact]
    public void ApproveClaim_WhileDraft_Throws()
    {
        var analysis = NewAnalysisWithClaim();
        var act = () => analysis.ApproveClaim(analysis.Claims[0].Id, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<InvalidMechanicAnalysisStateException>();
    }

    [Fact]
    public void RejectClaim_WithoutNote_Throws()
    {
        var analysis = NewAnalysisWithClaim();
        analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);

        var act = () => analysis.RejectClaim(analysis.Claims[0].Id, Guid.NewGuid(), "  ", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    // ============================================================
    // AC-12 / T5: Suppression kill-switch
    // ============================================================

    [Fact]
    public void Suppress_FromPublished_SetsFields_AndRaisesEvent()
    {
        var analysis = PublishedAnalysis(out _, out _);
        analysis.ClearDomainEvents();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        analysis.Suppress(actor, "DMCA", SuppressionRequestSource.Legal, requestedAt: now.AddHours(-2), utcNow: now);

        analysis.IsSuppressed.Should().BeTrue();
        analysis.SuppressedBy.Should().Be(actor);
        analysis.SuppressedAt.Should().Be(now);
        analysis.SuppressionReason.Should().Be("DMCA");
        analysis.SuppressionRequestSource.Should().Be(SuppressionRequestSource.Legal);
        analysis.Status.Should().Be(MechanicAnalysisStatus.Published); // orthogonal
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisSuppressedEvent>();
    }

    [Fact]
    public void Suppress_WhenAlreadySuppressed_Throws()
    {
        var analysis = PublishedAnalysis(out _, out _);
        analysis.Suppress(Guid.NewGuid(), "r1", SuppressionRequestSource.Other, null, DateTime.UtcNow);

        var act = () => analysis.Suppress(Guid.NewGuid(), "r2", SuppressionRequestSource.Other, null, DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already suppressed*");
    }

    [Fact]
    public void Unsuppress_ClearsFields_AndRaisesEvent()
    {
        var analysis = PublishedAnalysis(out _, out _);
        analysis.Suppress(Guid.NewGuid(), "r", SuppressionRequestSource.Other, null, DateTime.UtcNow);
        analysis.ClearDomainEvents();

        analysis.Unsuppress(Guid.NewGuid(), "appeal upheld", DateTime.UtcNow);

        analysis.IsSuppressed.Should().BeFalse();
        analysis.SuppressedBy.Should().BeNull();
        analysis.SuppressedAt.Should().BeNull();
        analysis.SuppressionReason.Should().BeNull();
        analysis.SuppressionRequestSource.Should().BeNull();
        analysis.SuppressionRequestedAt.Should().BeNull();
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisUnsuppressedEvent>();
    }

    [Fact]
    public void Unsuppress_WhenNotSuppressed_Throws()
    {
        var analysis = PublishedAnalysis(out _, out _);
        var act = () => analysis.Unsuppress(Guid.NewGuid(), "reason", DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not suppressed*");
    }

    // ============================================================
    // T8: Cost cap override
    // ============================================================

    [Fact]
    public void OverrideCostCap_WithHigherCap_SetsAllThreeOverrideFields_AndRaisesEvent()
    {
        var analysis = NewAnalysisWithClaim(costCap: 1m);
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        analysis.OverrideCostCap(actor, newCapUsd: 5m, reason: "high-cost game", utcNow: now);

        analysis.CostCapUsd.Should().Be(5m);
        analysis.CostCapOverrideAt.Should().Be(now);
        analysis.CostCapOverrideBy.Should().Be(actor);
        analysis.CostCapOverrideReason.Should().Be("high-cost game");
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisCostCapOverriddenEvent>();
    }

    [Fact]
    public void OverrideCostCap_WithLowerOrEqualCap_Throws()
    {
        var analysis = NewAnalysisWithClaim(costCap: 5m);
        var act = () => analysis.OverrideCostCap(Guid.NewGuid(), newCapUsd: 5m, reason: "r", utcNow: DateTime.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // P1' (ADR-051 Sprint 2) — PartiallyExtracted status & checkpoint
    // ============================================================
    //
    // Context: the executor's ApplyAbort path historically called AutoRejectFromDraft for any
    // mid-pipeline failure (cost cap, LLM failure, validation beyond retry). That discarded any
    // claims successfully parsed from earlier sections — a bad bargain when sections 1-3 of 6
    // produced clean claims but section 4 ran into the cost cap. The relaxed atomicity invariant
    // (see ADR-051 addendum 2026-04-26) lets the executor checkpoint partial work into a new
    // PartiallyExtracted state, distinct from Rejected so admins can triage what survived.
    //
    // Transitions added:
    //   Draft → PartiallyExtracted        : MarkAsPartiallyExtracted (system-initiated, claims
    //                                       already parsed and added)
    //   PartiallyExtracted → InReview     : SubmitForReview (admin promotes partial set to review)
    //   PartiallyExtracted → Rejected     : Reject (admin discards the salvage)
    //
    // The reason string mirrors AutoRejectFromDraft's vocabulary (cost_cap_exceeded etc.) and is
    // stored in RejectionReason for audit; SubmitForReview clears it on promotion the same way it
    // does for the Rejected → InReview resubmission path.

    [Fact]
    public void MarkAsPartiallyExtracted_FromDraft_TransitionsAndPreservesClaims_AndRaisesEvent()
    {
        var analysis = NewAnalysisWithClaim();
        var claimsBefore = analysis.Claims.Select(c => c.Id).ToArray();
        var actor = analysis.CreatedBy; // executor records the run initiator as actor (T6 audit)
        var now = DateTime.UtcNow;

        analysis.MarkAsPartiallyExtracted(
            MechanicAnalysis.AutoRejectionReasons.CostCapExceeded,
            actor,
            now);

        analysis.Status.Should().Be(MechanicAnalysisStatus.PartiallyExtracted);
        analysis.Claims.Select(c => c.Id).Should().BeEquivalentTo(claimsBefore);
        analysis.RejectionReason.Should().Be(MechanicAnalysis.AutoRejectionReasons.CostCapExceeded);
        analysis.ReviewedBy.Should().Be(actor);
        analysis.ReviewedAt.Should().Be(now);
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisStatusChangedEvent>()
            .Which.ToStatus.Should().Be(MechanicAnalysisStatus.PartiallyExtracted);
    }

    [Theory]
    [InlineData(MechanicAnalysisStatus.InReview)]
    [InlineData(MechanicAnalysisStatus.Published)]
    [InlineData(MechanicAnalysisStatus.Rejected)]
    public void MarkAsPartiallyExtracted_FromNonDraft_Throws(MechanicAnalysisStatus startingStatus)
    {
        var analysis = NewAnalysisWithClaim();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Drive to the requested state via the public state machine.
        switch (startingStatus)
        {
            case MechanicAnalysisStatus.InReview:
                analysis.SubmitForReview(actor, now);
                break;
            case MechanicAnalysisStatus.Published:
                analysis.SubmitForReview(actor, now);
                analysis.ApproveClaim(analysis.Claims[0].Id, actor, now);
                analysis.Approve(actor, now);
                break;
            case MechanicAnalysisStatus.Rejected:
                analysis.SubmitForReview(actor, now);
                analysis.Reject(actor, "human reject", now);
                break;
        }

        var act = () => analysis.MarkAsPartiallyExtracted(
            MechanicAnalysis.AutoRejectionReasons.CostCapExceeded,
            actor,
            now);

        act.Should().Throw<InvalidMechanicAnalysisStateException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkAsPartiallyExtracted_WithBlankReason_Throws(string reason)
    {
        var analysis = NewAnalysisWithClaim();
        var act = () => analysis.MarkAsPartiallyExtracted(reason, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*reason*");
    }

    [Fact]
    public void MarkAsPartiallyExtracted_WithEmptyActor_Throws()
    {
        var analysis = NewAnalysisWithClaim();
        var act = () => analysis.MarkAsPartiallyExtracted(
            MechanicAnalysis.AutoRejectionReasons.CostCapExceeded,
            Guid.Empty,
            DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*ActorId*");
    }

    [Fact]
    public void SubmitForReview_FromPartiallyExtracted_TransitionsToInReview_AndClearsRejectionReason()
    {
        var analysis = NewAnalysisWithClaim();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;
        analysis.MarkAsPartiallyExtracted(
            MechanicAnalysis.AutoRejectionReasons.CostCapExceeded,
            analysis.CreatedBy,
            now);
        // Claims are still Pending — MarkAsPartiallyExtracted leaves the per-claim status alone
        // (unlike Rejected → SubmitForReview, which has to re-pend Approved/Rejected claims).
        analysis.Claims[0].Status.Should().Be(MechanicClaimStatus.Pending);
        analysis.ClearDomainEvents();

        analysis.SubmitForReview(actor, now);

        analysis.Status.Should().Be(MechanicAnalysisStatus.InReview);
        analysis.RejectionReason.Should().BeNull(
            "the abort reason was a system-initiated marker, not a human review verdict — it must be cleared on promotion to review");
        analysis.Claims[0].Status.Should().Be(MechanicClaimStatus.Pending);
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisStatusChangedEvent>();
    }

    [Fact]
    public void Reject_FromPartiallyExtracted_TransitionsToRejected_WithReason()
    {
        var analysis = NewAnalysisWithClaim();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;
        analysis.MarkAsPartiallyExtracted(
            MechanicAnalysis.AutoRejectionReasons.CostCapExceeded,
            analysis.CreatedBy,
            now);
        analysis.ClearDomainEvents();

        analysis.Reject(actor, "salvage not worth keeping", now);

        analysis.Status.Should().Be(MechanicAnalysisStatus.Rejected);
        analysis.RejectionReason.Should().Be("salvage not worth keeping");
        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MechanicAnalysisStatusChangedEvent>();
    }

    [Fact]
    public void AddClaim_AfterMarkAsPartiallyExtracted_Throws()
    {
        // The executor's contract is "extract all sections, then checkpoint". Once partial, the
        // aggregate must reject further claim additions just like any non-Draft state.
        var analysis = NewAnalysisWithClaim();
        analysis.MarkAsPartiallyExtracted(
            MechanicAnalysis.AutoRejectionReasons.CostCapExceeded,
            analysis.CreatedBy,
            DateTime.UtcNow);

        var stranded = MechanicClaim.Create(
            analysisId: analysis.Id,
            section: MechanicSection.Faq,
            text: "Stranded claim",
            displayOrder: 99,
            citations: new[]
            {
                MechanicCitation.Create(analysis.Id, 1, "Quote.", null, 0)
            });

        var act = () => analysis.AddClaim(stranded);
        act.Should().Throw<InvalidMechanicAnalysisStateException>();
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static MechanicAnalysis NewAnalysisWithClaim(decimal costCap = 1m)
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: costCap);

        // MechanicCitation.Create validates ClaimId != Empty only (not relational integrity).
        // The real DB FK is enforced by the EF configuration. For domain tests we reuse analysis.Id
        // as a non-empty placeholder for ClaimId — MechanicClaim is created next and its real Id is
        // independent, but the citation's ClaimId field is irrelevant for aggregate-level behavior.
        var claim = MechanicClaim.Create(
            analysisId: analysis.Id,
            section: MechanicSection.Mechanics,
            text: "Draw phase",
            displayOrder: 0,
            citations: new[]
            {
                MechanicCitation.Create(
                    claimId: analysis.Id, // dummy — DB FK enforced at persistence, domain only checks non-empty
                    pdfPage: 1,
                    quote: "Each turn draw one card.",
                    chunkId: null,
                    displayOrder: 0)
            });

        analysis.AddClaim(claim);
        return analysis;
    }

    private static MechanicAnalysis PublishedAnalysis(out Guid reviewerId, out DateTime utcNow)
    {
        var analysis = NewAnalysisWithClaim();
        reviewerId = Guid.NewGuid();
        utcNow = DateTime.UtcNow;
        analysis.SubmitForReview(reviewerId, utcNow);
        analysis.ApproveClaim(analysis.Claims[0].Id, reviewerId, utcNow);
        analysis.Approve(reviewerId, utcNow);
        return analysis;
    }
}
