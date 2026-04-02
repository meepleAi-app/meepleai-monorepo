using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Unit tests for KbCoverageComputeJob static scoring helpers.
/// KB-05: Daily coverage score batch job.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbCoverageComputeJobTests
{
    // ── ComputeScore ──────────────────────────────────────────────────────────

    [Fact]
    public void ComputeCoverageScore_NoDocuments_ReturnsZero()
    {
        var score = KbCoverageComputeJob.ComputeScore(
            hasRulebook: false, hasErrata: false, hasFaq: false,
            completedChunks: 0, totalChunks: 0, daysSinceLastIndex: 999);

        Assert.Equal(0, score);
    }

    [Fact]
    public void ComputeCoverageScore_OnlyRulebook_Returns50()
    {
        // hasRulebook = +50; no errata/faq; 0 chunks; stale (>90 days)
        var score = KbCoverageComputeJob.ComputeScore(
            hasRulebook: true, hasErrata: false, hasFaq: false,
            completedChunks: 0, totalChunks: 0, daysSinceLastIndex: 100);

        Assert.Equal(50, score);
    }

    [Fact]
    public void ComputeCoverageScore_FullCoverage_Returns100()
    {
        // hasRulebook = 50 + hasFaq = 15 + hasErrata = 5 + chunks (200/200 * 20 = 20) + fresh = 10 → 100
        var score = KbCoverageComputeJob.ComputeScore(
            hasRulebook: true, hasErrata: true, hasFaq: true,
            completedChunks: 200, totalChunks: 200, daysSinceLastIndex: 30);

        Assert.Equal(100, score);
    }

    [Fact]
    public void ComputeCoverageScore_ChunkDensity_SaturatesAt200Chunks()
    {
        // 400 completed out of 400 → capped at 1.0 * 20 = 20
        var score400 = KbCoverageComputeJob.ComputeScore(
            hasRulebook: false, hasErrata: false, hasFaq: false,
            completedChunks: 400, totalChunks: 400, daysSinceLastIndex: 999);

        var score200 = KbCoverageComputeJob.ComputeScore(
            hasRulebook: false, hasErrata: false, hasFaq: false,
            completedChunks: 200, totalChunks: 200, daysSinceLastIndex: 999);

        Assert.Equal(score200, score400);
        Assert.Equal(20, score200);
    }

    [Fact]
    public void ComputeCoverageScore_FreshnessBonus_AppliedWithin90Days()
    {
        var scoreOld = KbCoverageComputeJob.ComputeScore(
            hasRulebook: false, hasErrata: false, hasFaq: false,
            completedChunks: 0, totalChunks: 0, daysSinceLastIndex: 91);

        var scoreFresh = KbCoverageComputeJob.ComputeScore(
            hasRulebook: false, hasErrata: false, hasFaq: false,
            completedChunks: 0, totalChunks: 0, daysSinceLastIndex: 90);

        Assert.Equal(0, scoreOld);
        Assert.Equal(10, scoreFresh);
    }

    [Fact]
    public void ComputeCoverageScore_NeverExceeds100()
    {
        // All flags + max chunks + fresh → should be capped at 100
        var score = KbCoverageComputeJob.ComputeScore(
            hasRulebook: true, hasErrata: true, hasFaq: true,
            completedChunks: 1000, totalChunks: 1000, daysSinceLastIndex: 1);

        Assert.True(score <= 100);
    }

    // ── ScoreToLevel ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "None")]
    [InlineData(24, "None")]
    [InlineData(25, "Basic")]
    [InlineData(49, "Basic")]
    [InlineData(50, "Standard")]
    [InlineData(74, "Standard")]
    [InlineData(75, "Complete")]
    [InlineData(100, "Complete")]
    public void CoverageLevel_ReturnsCorrectLabel(int score, string expected)
    {
        var level = KbCoverageComputeJob.ScoreToLevel(score);
        Assert.Equal(expected, level);
    }
}
