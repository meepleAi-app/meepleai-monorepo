using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Tests for RoutingMetricsCollector - thread-safe metrics tracking.
/// Issue #4336: Multi-Agent Router - Routing Metrics.
/// </summary>
public class RoutingMetricsCollectorTests
{
    private readonly RoutingMetricsCollector _collector;

    public RoutingMetricsCollectorTests()
    {
        _collector = new RoutingMetricsCollector(
            NullLogger<RoutingMetricsCollector>.Instance);
    }

    [Fact]
    public void GetSnapshot_NoDecisions_ReturnsZeros()
    {
        var snapshot = _collector.GetSnapshot();

        snapshot.TotalDecisions.Should().Be(0);
        snapshot.AverageConfidence.Should().Be(0.0);
        snapshot.AverageRoutingLatencyMs.Should().Be(0.0);
        snapshot.FallbackRate.Should().Be(0.0);
    }

    [Fact]
    public void RecordRoutingDecision_SingleDecision_IncrementsTotalCount()
    {
        var decision = CreateDecision("TutorAgent", AgentIntent.Tutorial, 0.95);

        _collector.RecordRoutingDecision(decision);
        var snapshot = _collector.GetSnapshot();

        snapshot.TotalDecisions.Should().Be(1);
    }

    [Fact]
    public void RecordRoutingDecision_HighConfidence_IncrementsHighConfidenceCount()
    {
        var decision = CreateDecision("TutorAgent", AgentIntent.Tutorial, 0.95);

        _collector.RecordRoutingDecision(decision);
        var snapshot = _collector.GetSnapshot();

        snapshot.HighConfidenceCount.Should().Be(1);
        snapshot.MediumConfidenceCount.Should().Be(0);
        snapshot.LowConfidenceCount.Should().Be(0);
    }

    [Fact]
    public void RecordRoutingDecision_MediumConfidence_IncrementsMediumCount()
    {
        var decision = CreateDecision("TutorAgent", AgentIntent.RulesQuestion, 0.80);

        _collector.RecordRoutingDecision(decision);
        var snapshot = _collector.GetSnapshot();

        snapshot.HighConfidenceCount.Should().Be(0);
        snapshot.MediumConfidenceCount.Should().Be(1);
    }

    [Fact]
    public void RecordRoutingDecision_LowConfidence_IncrementsLowCount()
    {
        var decision = CreateDecision("TutorAgent", AgentIntent.Unknown, 0.50,
            fallbackAgents: ["TutorAgent", "ArbitroAgent"]);

        _collector.RecordRoutingDecision(decision);
        var snapshot = _collector.GetSnapshot();

        snapshot.HighConfidenceCount.Should().Be(0);
        snapshot.MediumConfidenceCount.Should().Be(0);
        snapshot.LowConfidenceCount.Should().Be(1);
    }

    [Fact]
    public void RecordRoutingDecision_WithFallback_IncrementsFallbackCount()
    {
        var decision = CreateDecision("TutorAgent", AgentIntent.Unknown, 0.50,
            fallbackAgents: ["TutorAgent", "ArbitroAgent"]);

        _collector.RecordRoutingDecision(decision);
        var snapshot = _collector.GetSnapshot();

        snapshot.FallbackCount.Should().Be(1);
        snapshot.FallbackRate.Should().Be(1.0);
    }

    [Fact]
    public void RecordRoutingDecision_MultipleDecisions_CalculatesAverages()
    {
        _collector.RecordRoutingDecision(CreateDecision("ArbitroAgent", AgentIntent.MoveValidation, 0.95));
        _collector.RecordRoutingDecision(CreateDecision("TutorAgent", AgentIntent.Tutorial, 0.85));

        var snapshot = _collector.GetSnapshot();

        snapshot.TotalDecisions.Should().Be(2);
        snapshot.AverageConfidence.Should().BeApproximately(0.90, 2);
    }

    [Fact]
    public void RecordRoutingDecision_TracksAgentUsageDistribution()
    {
        _collector.RecordRoutingDecision(CreateDecision("ArbitroAgent", AgentIntent.MoveValidation, 0.95));
        _collector.RecordRoutingDecision(CreateDecision("ArbitroAgent", AgentIntent.MoveValidation, 0.90));
        _collector.RecordRoutingDecision(CreateDecision("TutorAgent", AgentIntent.Tutorial, 0.85));

        var snapshot = _collector.GetSnapshot();

        snapshot.AgentUsageDistribution["ArbitroAgent"].Should().Be(2);
        snapshot.AgentUsageDistribution["TutorAgent"].Should().Be(1);
    }

    [Fact]
    public void RecordRoutingDecision_TracksIntentDistribution()
    {
        _collector.RecordRoutingDecision(CreateDecision("ArbitroAgent", AgentIntent.MoveValidation, 0.95));
        _collector.RecordRoutingDecision(CreateDecision("DecisoreAgent", AgentIntent.StrategicAnalysis, 0.90));
        _collector.RecordRoutingDecision(CreateDecision("TutorAgent", AgentIntent.Tutorial, 0.85));

        var snapshot = _collector.GetSnapshot();

        snapshot.IntentDistribution[AgentIntent.MoveValidation].Should().Be(1);
        snapshot.IntentDistribution[AgentIntent.StrategicAnalysis].Should().Be(1);
        snapshot.IntentDistribution[AgentIntent.Tutorial].Should().Be(1);
    }

    [Fact]
    public void RecordRoutingDecision_ThreadSafety_ConcurrentAccess()
    {
        const int threadCount = 10;
        const int decisionsPerThread = 100;

        var tasks = Enumerable.Range(0, threadCount).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < decisionsPerThread; i++)
                {
                    _collector.RecordRoutingDecision(
                        CreateDecision("TutorAgent", AgentIntent.Tutorial, 0.85));
                }
            }));

        Task.WaitAll(tasks.ToArray());
        var snapshot = _collector.GetSnapshot();

        snapshot.TotalDecisions.Should().Be(threadCount * decisionsPerThread);
    }

    private static AgentRoutingDecision CreateDecision(
        string targetAgent,
        AgentIntent intent,
        double confidence,
        List<string>? fallbackAgents = null)
    {
        return new AgentRoutingDecision(
            TargetAgent: targetAgent,
            Intent: intent,
            Confidence: confidence,
            ShouldRoute: confidence >= 0.90,
            RequiresConfirmation: confidence >= 0.70 && confidence < 0.90,
            FallbackAgents: fallbackAgents,
            ClassificationDuration: TimeSpan.FromMilliseconds(1),
            RoutingDuration: TimeSpan.FromMilliseconds(2),
            AllIntentScores: [new IntentScore(intent, confidence)]);
    }
}
