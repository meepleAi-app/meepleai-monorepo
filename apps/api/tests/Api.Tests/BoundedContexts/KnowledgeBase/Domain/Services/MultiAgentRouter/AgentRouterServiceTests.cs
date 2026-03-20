using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Tests for AgentRouterService - routing decision logic with confidence thresholds.
/// Issue #4336: Multi-Agent Router - Routing Logic.
/// </summary>
public class AgentRouterServiceTests
{
    private readonly AgentRouterService _router;
    private readonly RoutingMetricsCollector _metricsCollector;

    public AgentRouterServiceTests()
    {
        var classifier = new IntentClassifier();
        _metricsCollector = new RoutingMetricsCollector(
            NullLogger<RoutingMetricsCollector>.Instance);
        _router = new AgentRouterService(
            classifier,
            _metricsCollector,
            NullLogger<AgentRouterService>.Instance);
    }

    #region Routing to Correct Agent

    [Theory]
    [InlineData("validate move e2 to e4", "ArbitroAgent")]
    [InlineData("is this move legal?", "ArbitroAgent")]
    [InlineData("suggest the best move", "StrategaAgent")]
    [InlineData("what should i do next?", "StrategaAgent")]
    [InlineData("teach me how to play", "TutorAgent")]
    [InlineData("tutorial for beginners", "TutorAgent")]
    [InlineData("what are the rules?", "TutorAgent")]
    [InlineData("how does turn order work?", "TutorAgent")]
    public void RouteQuery_ClearIntent_RoutesToCorrectAgent(string query, string expectedAgent)
    {
        var decision = _router.RouteQuery(query);

        decision.TargetAgent.Should().Be(expectedAgent);
    }

    #endregion

    #region Confidence Thresholds

    [Fact]
    public void RouteQuery_HighConfidence_ShouldRouteTrue()
    {
        var decision = _router.RouteQuery("validate move to e4");

        decision.ShouldRoute.Should().BeTrue($"Expected ShouldRoute=true for high confidence ({decision.Confidence:F3})");
        decision.RequiresConfirmation.Should().BeFalse();
        decision.FallbackAgents.Should().BeNull();
    }

    [Fact]
    public void RouteQuery_UnknownIntent_HasFallbackAgents()
    {
        var decision = _router.RouteQuery("hello, what's going on?");

        decision.FallbackAgents.Should().NotBeNull();
        decision.FallbackAgents.Should().NotBeEmpty();
    }

    [Fact]
    public void RouteQuery_UnknownIntent_DefaultsToTutor()
    {
        var decision = _router.RouteQuery("random question about nothing");

        decision.TargetAgent.Should().Be("TutorAgent");
    }

    #endregion

    #region Routing Duration

    [Fact]
    public void RouteQuery_PerformanceRequirement_RoutingUnder50ms()
    {
        var decision = _router.RouteQuery("validate move e2 to e4");

        (decision.RoutingDuration.TotalMilliseconds < 50).Should().BeTrue($"Routing took {decision.RoutingDuration.TotalMilliseconds:F1}ms, expected <50ms P95");
    }

    [Fact]
    public void RouteQuery_IncludesClassificationDuration()
    {
        var decision = _router.RouteQuery("suggest best move");

        (decision.ClassificationDuration.TotalMilliseconds >= 0).Should().BeTrue();
        (decision.RoutingDuration >= decision.ClassificationDuration).Should().BeTrue();
    }

    #endregion

    #region Intent Scores Transparency

    [Fact]
    public void RouteQuery_ReturnsAllIntentScores()
    {
        var decision = _router.RouteQuery("validate move");

        decision.AllIntentScores.Should().NotBeNull();
        decision.AllIntentScores.Should().NotBeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RouteQuery_EmptyQuery_ThrowsArgumentException()
    {
        ((Action)(() => _router.RouteQuery(""))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RouteQuery_WhitespaceQuery_ThrowsArgumentException()
    {
        ((Action)(() => _router.RouteQuery("   "))).Should().Throw<ArgumentException>();
    }

    #endregion

    #region Metrics Recording

    [Fact]
    public void RouteQuery_RecordsMetrics()
    {
        _router.RouteQuery("validate move");
        _router.RouteQuery("teach me chess");
        _router.RouteQuery("suggest best move");

        var snapshot = _metricsCollector.GetSnapshot();

        snapshot.TotalDecisions.Should().Be(3);
        (snapshot.AverageConfidence > 0).Should().BeTrue();
        (snapshot.AverageRoutingLatencyMs >= 0).Should().BeTrue();
    }

    [Fact]
    public void RouteQuery_TracksAgentDistribution()
    {
        _router.RouteQuery("validate move to e4");
        _router.RouteQuery("is this move legal?");
        _router.RouteQuery("teach me chess");

        var snapshot = _metricsCollector.GetSnapshot();

        snapshot.AgentUsageDistribution.ContainsKey("ArbitroAgent").Should().BeTrue();
        snapshot.AgentUsageDistribution.ContainsKey("TutorAgent").Should().BeTrue();
    }

    [Fact]
    public void RouteQuery_TracksIntentDistribution()
    {
        _router.RouteQuery("validate move");
        _router.RouteQuery("suggest best move");

        var snapshot = _metricsCollector.GetSnapshot();

        snapshot.IntentDistribution.ContainsKey(AgentIntent.MoveValidation).Should().BeTrue();
        snapshot.IntentDistribution.ContainsKey(AgentIntent.StrategicAnalysis).Should().BeTrue();
    }

    #endregion
}
