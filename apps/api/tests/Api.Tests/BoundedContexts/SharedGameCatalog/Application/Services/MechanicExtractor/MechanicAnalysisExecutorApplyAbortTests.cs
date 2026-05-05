using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Infrastructure.Entities.SharedGameCatalog;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Unit tests for <see cref="MechanicAnalysisExecutor.ApplyAbort"/> — the partial-recovery
/// checkpoint introduced in ADR-051 Sprint 2 (P1'). Asserts the dual-branch behaviour:
///   1. SectionOutputs contain at least one parseable section → analysis transitions to
///      <see cref="MechanicAnalysisStatus.PartiallyExtracted"/>, claims are salvaged, and
///      cost telemetry is recorded BEFORE the terminal transition.
///   2. SectionOutputs are empty or unparseable → fallback to <see cref="MechanicAnalysisStatus.Rejected"/>
///      via the original <c>AutoRejectFromDraft</c> path, with telemetry preserved.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisExecutorApplyAbortTests
{
    // Well-formed Summary envelope — one citation, ≤25-word quote.
    private const string ParseableSummaryJson = """
        {
          "summary": {
            "text": "Catan is a resource-trading game where settlements convert raw materials into victory points.",
            "citations": [
              { "pdf_page": 1, "quote": "Trade resources to build settlements." }
            ]
          }
        }
        """;

    // Well-formed Mechanics envelope — produces 2 claims.
    private const string ParseableMechanicsJson = """
        {
          "mechanics": [
            {
              "name": "Resource production",
              "description": "Each terrain hex produces a resource when its number is rolled.",
              "citations": [{ "pdf_page": 3, "quote": "Roll the dice and collect resources." }]
            },
            {
              "name": "Robber",
              "description": "On rolling a 7, the robber blocks production on a hex.",
              "citations": [{ "pdf_page": 5, "quote": "Move the robber on a seven." }]
            }
          ]
        }
        """;

    // Malformed JSON — parser must drop silently.
    private const string MalformedJson = "{ this is not json :: ::: }";

    [Fact]
    public void ApplyAbort_AbortedCostCap_WithParseableSummary_TransitionsToPartiallyExtracted_AndSalvagesClaims()
    {
        var analysis = NewDraftAnalysis();
        var now = DateTime.UtcNow;

        var result = BuildAbortResult(
            outcome: MechanicPipelineOutcome.AbortedCostCap,
            sectionOutputs: new Dictionary<MechanicSection, string>
            {
                [MechanicSection.Summary] = ParseableSummaryJson
            },
            totalPromptTokens: 1_500,
            totalCompletionTokens: 800,
            totalCostUsd: 0.0024m);

        var summary = MechanicAnalysisExecutor.ApplyAbort(analysis, result, now);

        summary.IsPartialCheckpoint.Should().BeTrue();
        summary.PartialClaimCount.Should().BeGreaterThan(0);
        summary.Reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.CostCapExceeded);

        analysis.Status.Should().Be(MechanicAnalysisStatus.PartiallyExtracted);
        analysis.Claims.Should().HaveCount(summary.PartialClaimCount);
        analysis.RejectionReason.Should().Be(MechanicAnalysis.AutoRejectionReasons.CostCapExceeded);
        analysis.ReviewedAt.Should().Be(now);
        analysis.ReviewedBy.Should().Be(analysis.CreatedBy);

        // Cost telemetry preserved before terminal transition (T23 invariant).
        analysis.TotalTokensUsed.Should().Be(2_300);
        analysis.EstimatedCostUsd.Should().Be(0.0024m);

        analysis.DomainEvents.OfType<MechanicAnalysisStatusChangedEvent>()
            .Should().ContainSingle()
            .Which.ToStatus.Should().Be(MechanicAnalysisStatus.PartiallyExtracted);
    }

    [Fact]
    public void ApplyAbort_AbortedCostCap_WithMultipleParseableSections_AggregatesAllSurvivingClaims()
    {
        var analysis = NewDraftAnalysis();
        var now = DateTime.UtcNow;

        var result = BuildAbortResult(
            outcome: MechanicPipelineOutcome.AbortedCostCap,
            sectionOutputs: new Dictionary<MechanicSection, string>
            {
                [MechanicSection.Summary] = ParseableSummaryJson,
                [MechanicSection.Mechanics] = ParseableMechanicsJson
            });

        var summary = MechanicAnalysisExecutor.ApplyAbort(analysis, result, now);

        summary.IsPartialCheckpoint.Should().BeTrue();
        // Summary contributes 1 synthetic claim; Mechanics contributes 2 → at least 3 total.
        summary.PartialClaimCount.Should().BeGreaterThanOrEqualTo(3);
        analysis.Claims.Should().HaveCount(summary.PartialClaimCount);
        analysis.Status.Should().Be(MechanicAnalysisStatus.PartiallyExtracted);
    }

    [Fact]
    public void ApplyAbort_AbortedLlmFailed_WithEmptySectionOutputs_FallsBackToRejected()
    {
        var analysis = NewDraftAnalysis();
        var now = DateTime.UtcNow;

        var result = BuildAbortResult(
            outcome: MechanicPipelineOutcome.AbortedLlmFailed,
            sectionOutputs: new Dictionary<MechanicSection, string>(),
            totalPromptTokens: 600,
            totalCompletionTokens: 0,
            totalCostUsd: 0.0002m);

        var summary = MechanicAnalysisExecutor.ApplyAbort(analysis, result, now);

        summary.IsPartialCheckpoint.Should().BeFalse();
        summary.PartialClaimCount.Should().Be(0);
        summary.Reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed);

        analysis.Status.Should().Be(MechanicAnalysisStatus.Rejected);
        analysis.Claims.Should().BeEmpty();
        analysis.RejectionReason.Should().Be(MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed);

        // Telemetry recorded even on full-abort path.
        analysis.TotalTokensUsed.Should().Be(600);
        analysis.EstimatedCostUsd.Should().Be(0.0002m);
    }

    [Fact]
    public void ApplyAbort_AbortedValidation_WithMalformedJson_FallsBackToRejected()
    {
        var analysis = NewDraftAnalysis();
        var now = DateTime.UtcNow;

        var result = BuildAbortResult(
            outcome: MechanicPipelineOutcome.AbortedValidation,
            sectionOutputs: new Dictionary<MechanicSection, string>
            {
                [MechanicSection.Summary] = MalformedJson,
                [MechanicSection.Mechanics] = MalformedJson
            });

        var summary = MechanicAnalysisExecutor.ApplyAbort(analysis, result, now);

        summary.IsPartialCheckpoint.Should().BeFalse();
        summary.PartialClaimCount.Should().Be(0);
        summary.Reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.ValidationFailedBeyondRetry);

        analysis.Status.Should().Be(MechanicAnalysisStatus.Rejected);
        analysis.Claims.Should().BeEmpty();
        analysis.RejectionReason.Should().Be(MechanicAnalysis.AutoRejectionReasons.ValidationFailedBeyondRetry);
    }

    [Fact]
    public void ApplyAbort_AbortedCostCap_RecordsUsageBeforeTerminalTransition_OnPartialCheckpoint()
    {
        var analysis = NewDraftAnalysis();
        analysis.TotalTokensUsed.Should().Be(0);
        analysis.EstimatedCostUsd.Should().Be(0m);

        var result = BuildAbortResult(
            outcome: MechanicPipelineOutcome.AbortedCostCap,
            sectionOutputs: new Dictionary<MechanicSection, string>
            {
                [MechanicSection.Summary] = ParseableSummaryJson
            },
            totalPromptTokens: 12_000,
            totalCompletionTokens: 4_000,
            totalCostUsd: 0.0188m);

        MechanicAnalysisExecutor.ApplyAbort(analysis, result, DateTime.UtcNow);

        analysis.TotalTokensUsed.Should().Be(16_000);
        analysis.EstimatedCostUsd.Should().Be(0.0188m);
    }

    [Fact]
    public void ApplyAbort_PartialCheckpoint_RaisesStatusChangedEvent_FromDraftToPartiallyExtracted()
    {
        var analysis = NewDraftAnalysis();

        var result = BuildAbortResult(
            outcome: MechanicPipelineOutcome.AbortedCostCap,
            sectionOutputs: new Dictionary<MechanicSection, string>
            {
                [MechanicSection.Summary] = ParseableSummaryJson
            });

        MechanicAnalysisExecutor.ApplyAbort(analysis, result, DateTime.UtcNow);

        var statusEvents = analysis.DomainEvents.OfType<MechanicAnalysisStatusChangedEvent>().ToList();
        statusEvents.Should().ContainSingle();
        statusEvents[0].FromStatus.Should().Be(MechanicAnalysisStatus.Draft);
        statusEvents[0].ToStatus.Should().Be(MechanicAnalysisStatus.PartiallyExtracted);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static MechanicAnalysis NewDraftAnalysis()
    {
        return MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);
    }

    private static MechanicPipelineResult BuildAbortResult(
        MechanicPipelineOutcome outcome,
        IReadOnlyDictionary<MechanicSection, string> sectionOutputs,
        int totalPromptTokens = 100,
        int totalCompletionTokens = 50,
        decimal totalCostUsd = 0.0001m)
    {
        return new MechanicPipelineResult(
            Outcome: outcome,
            SectionRuns: Array.Empty<MechanicAnalysisSectionRunEntity>(),
            SectionOutputs: sectionOutputs,
            TotalPromptTokens: totalPromptTokens,
            TotalCompletionTokens: totalCompletionTokens,
            TotalCostUsd: totalCostUsd,
            AbortDetail: $"test abort ({outcome})");
    }
}
