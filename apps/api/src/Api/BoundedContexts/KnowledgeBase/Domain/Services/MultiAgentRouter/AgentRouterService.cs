using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Routes user queries to appropriate agent based on intent classification.
/// Issue #4336: Multi-Agent Router - Request Routing Logic.
/// </summary>
internal sealed class AgentRouterService
{
    private readonly IntentClassifier _intentClassifier;
    private readonly RoutingMetricsCollector _metricsCollector;
    private readonly ILogger<AgentRouterService> _logger;

    internal const double HighConfidenceThreshold = 0.90;
    internal const double MediumConfidenceThreshold = 0.70;

    public AgentRouterService(
        IntentClassifier intentClassifier,
        RoutingMetricsCollector metricsCollector,
        ILogger<AgentRouterService> logger)
    {
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Routes a query to the appropriate agent, returning a routing decision with confidence.
    /// </summary>
    public AgentRoutingDecision RouteQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        var routingStopwatch = Stopwatch.StartNew();

        var classification = _intentClassifier.ClassifyQuery(query);
        var targetAgent = MapIntentToAgent(classification.Intent);
        var routingConfidence = CalculateRoutingConfidence(classification.Confidence, classification.Intent);

        routingStopwatch.Stop();

        _logger.LogInformation(
            "[Router] Query classified: intent={Intent}, confidence={Confidence:F3}, targetAgent={Agent}, classificationMs={ClassMs:F1}, routingMs={RouteMs:F1}",
            classification.Intent,
            routingConfidence,
            targetAgent,
            classification.ClassificationDuration.TotalMilliseconds,
            routingStopwatch.Elapsed.TotalMilliseconds);

        var decision = new AgentRoutingDecision(
            TargetAgent: targetAgent,
            Intent: classification.Intent,
            Confidence: routingConfidence,
            ShouldRoute: routingConfidence >= HighConfidenceThreshold,
            RequiresConfirmation: routingConfidence >= MediumConfidenceThreshold && routingConfidence < HighConfidenceThreshold,
            FallbackAgents: routingConfidence < MediumConfidenceThreshold ? GetFallbackAgents(classification.Intent) : null,
            ClassificationDuration: classification.ClassificationDuration,
            RoutingDuration: routingStopwatch.Elapsed,
            AllIntentScores: classification.AllScores
        );

        // Record metrics
        _metricsCollector.RecordRoutingDecision(decision);

        return decision;
    }

    private static string MapIntentToAgent(AgentIntent intent)
    {
        return intent switch
        {
            AgentIntent.Tutorial => "TutorAgent",
            AgentIntent.RulesQuestion => "TutorAgent",
            AgentIntent.MoveValidation => "ArbitroAgent",
            AgentIntent.StrategicAnalysis => "StrategaAgent",
            AgentIntent.Narrative => "NarratoreAgent",
            _ => "TutorAgent" // Default fallback
        };
    }

    private static double CalculateRoutingConfidence(double classificationConfidence, AgentIntent intent)
    {
        // Boost confidence for high-clarity intents
        var boost = intent switch
        {
            AgentIntent.MoveValidation => 0.05,
            AgentIntent.StrategicAnalysis => 0.05,
            AgentIntent.Narrative => 0.03,
            AgentIntent.Tutorial => 0.03,
            _ => 0.0
        };

        return Math.Min(classificationConfidence + boost, 1.0);
    }

    private static List<string> GetFallbackAgents(AgentIntent intent)
    {
        return intent switch
        {
            AgentIntent.Unknown => ["TutorAgent", "ArbitroAgent", "StrategaAgent"],
            AgentIntent.RulesQuestion => ["TutorAgent", "ArbitroAgent"],
            AgentIntent.MoveValidation => ["ArbitroAgent", "TutorAgent"],
            AgentIntent.StrategicAnalysis => ["StrategaAgent", "TutorAgent"],
            AgentIntent.Narrative => ["NarratoreAgent", "TutorAgent"],
            _ => ["TutorAgent"]
        };
    }
}

/// <summary>
/// Routing decision with confidence, fallback options, and performance metrics.
/// </summary>
internal sealed record AgentRoutingDecision(
    string TargetAgent,
    AgentIntent Intent,
    double Confidence,
    bool ShouldRoute,
    bool RequiresConfirmation,
    List<string>? FallbackAgents,
    TimeSpan ClassificationDuration,
    TimeSpan RoutingDuration,
    List<IntentScore> AllIntentScores);
