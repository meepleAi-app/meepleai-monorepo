using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class DocumentEvaluationRunTests
{
    private static readonly Guid DocId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AdminId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Create_WithoutReuseSeed_GeneratesRandomSeed()
    {
        var run1 = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: null);
        var run2 = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: null);

        run1.GoldsetGenerationSeed.Should().NotBe(0);
        run2.GoldsetGenerationSeed.Should().NotBe(0);
        run1.GoldsetGenerationSeed.Should().NotBe(run2.GoldsetGenerationSeed);
    }

    [Fact]
    public void Create_WithReuseSeed_PinsToProvidedValue()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: 42L);
        run.GoldsetGenerationSeed.Should().Be(42L);
    }

    [Fact]
    public void Create_SetsInitialState()
    {
        var before = DateTime.UtcNow;
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: null);
        var after = DateTime.UtcNow;

        run.Id.Should().NotBe(Guid.Empty);
        run.PdfDocumentId.Should().Be(DocId);
        run.GoldsetVersion.Should().Be("auto-v1");
        run.TriggeredByAdminId.Should().Be(AdminId);
        run.Status.Should().Be(EvaluationStatus.Pending);
        run.StartedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        run.CompletedAt.Should().BeNull();
        run.Metrics.Should().BeNull();
        run.CostUsd.Should().BeNull();
        run.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void TransitionTo_FromPending_AllowsGoldsetGenerating()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, null);
        run.TransitionTo(EvaluationStatus.GoldsetGenerating);
        run.Status.Should().Be(EvaluationStatus.GoldsetGenerating);
    }

    [Fact]
    public void MarkCompleted_SetsTerminalState()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, null);
        run.TransitionTo(EvaluationStatus.GoldsetGenerating);
        run.TransitionTo(EvaluationStatus.Running);

        var metrics = SampleMetrics();
        run.MarkCompleted(metrics, costUsd: 0.05m);

        run.Status.Should().Be(EvaluationStatus.Completed);
        run.CompletedAt.Should().NotBeNull();
        run.Metrics.Should().Be(metrics);
        run.CostUsd.Should().Be(0.05m);
    }

    [Fact]
    public void MarkFailed_SetsErrorMessage()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, null);
        run.MarkFailed("LLM timeout after 30s");

        run.Status.Should().Be(EvaluationStatus.Failed);
        run.ErrorMessage.Should().Be("LLM timeout after 30s");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void TransitionTo_FromTerminalState_Throws()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, null);
        run.MarkFailed("fail");

        var act = () => run.TransitionTo(EvaluationStatus.Running);

        act.Should().Throw<InvalidOperationException>();
    }

    private static EvaluationMetrics SampleMetrics() => new(
        Precision: new PrecisionMetrics(At1: 0.8, At3: 0.7, At5: 0.65),
        Ranking: new RankingMetrics(Mrr: 0.55),
        Latency: new LatencyMetrics(P50: TimeSpan.FromMilliseconds(120), P95: TimeSpan.FromMilliseconds(450)),
        QueryCount: 15,
        CostUsd: 0.05m,
        QualityBand: QualityBand.Yellow);
}
