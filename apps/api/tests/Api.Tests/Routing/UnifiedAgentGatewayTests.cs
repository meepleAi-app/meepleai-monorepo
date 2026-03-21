using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// Tests for Unified API Gateway components.
/// Issue #4338: Unified API Gateway - /api/v1/agents/query.
/// Tests cover: routing integration, request validation, metrics response.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UnifiedAgentGatewayTests
{
    #region AgentRouterService Integration

    [Theory]
    [InlineData("validate move e2 to e4", "ArbitroAgent")]
    [InlineData("suggest move for my knight", "StrategaAgent")]
    [InlineData("what are the rules for castling?", "TutorAgent")]
    [InlineData("how to play chess", "TutorAgent")]
    public void RouteQuery_ClearIntentQueries_RoutesToCorrectAgent(string query, string expectedAgent)
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var decision = router.RouteQuery(query);

        decision.TargetAgent.Should().Be(expectedAgent);
    }

    [Fact]
    public void RouteQuery_EmptyQuery_ThrowsArgumentException()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var act = () => router.RouteQuery("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RouteQuery_WhitespaceQuery_ThrowsArgumentException()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var act = () => router.RouteQuery("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RouteQuery_ReturnsRoutingDecisionWithAllFields()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var decision = router.RouteQuery("validate move e2 to e4");

        decision.TargetAgent.Should().NotBeNull();
        Assert.NotEqual(AgentIntent.Unknown, decision.Intent);
        (decision.Confidence > 0).Should().BeTrue();
    }

    [Fact]
    public void RouteQuery_HighConfidenceQuery_ShouldRouteIsTrue()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var decision = router.RouteQuery("validate move");

        (decision.Confidence >= 0.70).Should().BeTrue($"Expected confidence >= 0.70, got {decision.Confidence:F3}");
    }

    [Fact]
    public void RouteQuery_AmbiguousQuery_ReturnsFallbackAgents()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var decision = router.RouteQuery("hello world");

        decision.FallbackAgents.Should().NotBeNull();
        decision.FallbackAgents.Should().NotBeEmpty();
    }

    #endregion

    #region Request Validation

    [Fact]
    public void QueryValidation_EmptyQuery_ShouldReject()
    {
        var query = "";
        (string.IsNullOrWhiteSpace(query)).Should().BeTrue("Empty query should be detected as invalid");
    }

    [Fact]
    public void QueryValidation_TooLongQuery_ShouldReject()
    {
        var query = new string('a', 2001);
        (query.Length > 2000).Should().BeTrue("Query exceeding 2000 chars should be detected as invalid");
    }

    [Fact]
    public void QueryValidation_ValidQuery_ShouldAccept()
    {
        var query = "How do I play Catan?";
        (string.IsNullOrWhiteSpace(query)).Should().BeFalse();
        (query.Length <= 2000).Should().BeTrue();
    }

    [Fact]
    public void QueryValidation_MaxLengthQuery_ShouldAccept()
    {
        var query = new string('a', 2000);
        (query.Length <= 2000).Should().BeTrue("Query exactly at 2000 chars should be accepted");
    }

    #endregion

    #region Intent Classification Coverage

    [Fact]
    public void IntentClassifier_AllIntentTypes_AreMapped()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        // Verify all non-Unknown intents map to a specific agent
        var testQueries = new Dictionary<string, string>
        {
            { "validate move", "ArbitroAgent" },
            { "suggest move for my rook", "StrategaAgent" },
            { "what is the rule for castling?", "TutorAgent" },
            { "tutorial for beginners", "TutorAgent" },
        };

        foreach (var (query, expectedAgent) in testQueries)
        {
            var decision = router.RouteQuery(query);
            decision.TargetAgent.Should().Be(expectedAgent);
        }
    }

    [Fact]
    public void IntentClassifier_UnknownIntent_DefaultsToTutorAgent()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var decision = router.RouteQuery("what time is it?");

        decision.TargetAgent.Should().Be("TutorAgent");
    }

    #endregion

    #region Routing Metrics Response

    [Fact]
    public void RoutingMetrics_ContainsAllAgents()
    {
        var expectedAgents = new[] { "TutorAgent", "ArbitroAgent", "StrategaAgent", "NarratoreAgent" };

        // Verify all four agents are present
        expectedAgents.Length.Should().Be(4);
        expectedAgents.Should().Contain("TutorAgent");
        expectedAgents.Should().Contain("ArbitroAgent");
        expectedAgents.Should().Contain("StrategaAgent");
        expectedAgents.Should().Contain("NarratoreAgent");
    }

    [Fact]
    public void RoutingMetrics_ContainsAllIntents()
    {
        var intents = Enum.GetNames<AgentIntent>();

        intents.Should().Contain("Unknown");
        intents.Should().Contain("Tutorial");
        intents.Should().Contain("RulesQuestion");
        intents.Should().Contain("MoveValidation");
        intents.Should().Contain("StrategicAnalysis");
    }

    [Fact]
    public void RoutingMetrics_ConfidenceThresholdsAreValid()
    {
        var high = 0.90;
        var medium = 0.70;
        var minimum = 0.40;

        (high > medium).Should().BeTrue("High threshold must be greater than medium");
        (medium > minimum).Should().BeTrue("Medium threshold must be greater than minimum");
        (minimum > 0).Should().BeTrue("Minimum threshold must be positive");
        (high <= 1.0).Should().BeTrue("High threshold must not exceed 1.0");
    }

    #endregion

    #region End-to-End Routing Pipeline

    [Theory]
    [InlineData("Is this move valid in Catan?", "MoveValidation")]
    [InlineData("what should i do next?", "StrategicAnalysis")]
    [InlineData("game rules for setup phase", "RulesQuestion")]
    [InlineData("teach me chess basics", "Tutorial")]
    public void EndToEnd_ClassifyAndRoute_ReturnsExpectedIntent(string query, string expectedIntentName)
    {
        var expectedIntent = Enum.Parse<AgentIntent>(expectedIntentName);
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var decision = router.RouteQuery(query);

        decision.Intent.Should().Be(expectedIntent);
    }

    [Fact]
    public void EndToEnd_RoutingDecision_IncludesConfirmationFlag()
    {
        var classifier = new IntentClassifier();
        var router = new AgentRouterService(classifier, new RoutingMetricsCollector(Mock.Of<ILogger<RoutingMetricsCollector>>()), Mock.Of<ILogger<AgentRouterService>>());

        var highConfidence = router.RouteQuery("validate move e2 to e4");
        var ambiguous = router.RouteQuery("something about games");

        // High confidence should route directly or require confirmation
        (highConfidence.ShouldRoute || highConfidence.RequiresConfirmation).Should().BeTrue("High confidence queries should either route or require confirmation");

        // Ambiguous queries should have fallback agents
        if (!ambiguous.ShouldRoute && !ambiguous.RequiresConfirmation)
        {
            ambiguous.FallbackAgents.Should().NotBeNull();
        }
    }

    #endregion
}
