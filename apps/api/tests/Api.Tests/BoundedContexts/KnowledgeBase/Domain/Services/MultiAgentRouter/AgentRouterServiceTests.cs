using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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
    [InlineData("suggest the best move", "DecisoreAgent")]
    [InlineData("what should i do next?", "DecisoreAgent")]
    [InlineData("teach me how to play", "TutorAgent")]
    [InlineData("tutorial for beginners", "TutorAgent")]
    [InlineData("what are the rules?", "TutorAgent")]
    [InlineData("how does turn order work?", "TutorAgent")]
    public void RouteQuery_ClearIntent_RoutesToCorrectAgent(string query, string expectedAgent)
    {
        var decision = _router.RouteQuery(query);

        Assert.Equal(expectedAgent, decision.TargetAgent);
    }

    #endregion

    #region Confidence Thresholds

    [Fact]
    public void RouteQuery_HighConfidence_ShouldRouteTrue()
    {
        var decision = _router.RouteQuery("validate move to e4");

        Assert.True(decision.ShouldRoute,
            $"Expected ShouldRoute=true for high confidence ({decision.Confidence:F3})");
        Assert.False(decision.RequiresConfirmation);
        Assert.Null(decision.FallbackAgents);
    }

    [Fact]
    public void RouteQuery_UnknownIntent_HasFallbackAgents()
    {
        var decision = _router.RouteQuery("hello, what's going on?");

        Assert.NotNull(decision.FallbackAgents);
        Assert.NotEmpty(decision.FallbackAgents);
    }

    [Fact]
    public void RouteQuery_UnknownIntent_DefaultsToTutor()
    {
        var decision = _router.RouteQuery("random question about nothing");

        Assert.Equal("TutorAgent", decision.TargetAgent);
    }

    #endregion

    #region Routing Duration

    [Fact]
    public void RouteQuery_PerformanceRequirement_RoutingUnder50ms()
    {
        var decision = _router.RouteQuery("validate move e2 to e4");

        Assert.True(decision.RoutingDuration.TotalMilliseconds < 50,
            $"Routing took {decision.RoutingDuration.TotalMilliseconds:F1}ms, expected <50ms P95");
    }

    [Fact]
    public void RouteQuery_IncludesClassificationDuration()
    {
        var decision = _router.RouteQuery("suggest best move");

        Assert.True(decision.ClassificationDuration.TotalMilliseconds >= 0);
        Assert.True(decision.RoutingDuration >= decision.ClassificationDuration);
    }

    #endregion

    #region Intent Scores Transparency

    [Fact]
    public void RouteQuery_ReturnsAllIntentScores()
    {
        var decision = _router.RouteQuery("validate move");

        Assert.NotNull(decision.AllIntentScores);
        Assert.NotEmpty(decision.AllIntentScores);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RouteQuery_EmptyQuery_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _router.RouteQuery(""));
    }

    [Fact]
    public void RouteQuery_WhitespaceQuery_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _router.RouteQuery("   "));
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

        Assert.Equal(3, snapshot.TotalDecisions);
        Assert.True(snapshot.AverageConfidence > 0);
        Assert.True(snapshot.AverageRoutingLatencyMs >= 0);
    }

    [Fact]
    public void RouteQuery_TracksAgentDistribution()
    {
        _router.RouteQuery("validate move to e4");
        _router.RouteQuery("is this move legal?");
        _router.RouteQuery("teach me chess");

        var snapshot = _metricsCollector.GetSnapshot();

        Assert.True(snapshot.AgentUsageDistribution.ContainsKey("ArbitroAgent"));
        Assert.True(snapshot.AgentUsageDistribution.ContainsKey("TutorAgent"));
    }

    [Fact]
    public void RouteQuery_TracksIntentDistribution()
    {
        _router.RouteQuery("validate move");
        _router.RouteQuery("suggest best move");

        var snapshot = _metricsCollector.GetSnapshot();

        Assert.True(snapshot.IntentDistribution.ContainsKey(AgentIntent.MoveValidation));
        Assert.True(snapshot.IntentDistribution.ContainsKey(AgentIntent.StrategicAnalysis));
    }

    #endregion
}
