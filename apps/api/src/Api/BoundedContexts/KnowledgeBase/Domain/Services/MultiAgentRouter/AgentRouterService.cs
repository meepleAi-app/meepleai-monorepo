using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Routes user queries to appropriate agent based on intent.
/// Issue #4336: Multi-Agent Router - Request Routing Logic.
/// </summary>
internal sealed class AgentRouterService
{
    private readonly IntentClassifier _intentClassifier;
    private readonly ILogger<AgentRouterService> _logger;

    private const double HighConfidenceThreshold = 0.90;
    private const double MediumConfidenceThreshold = 0.70;

    public AgentRouterService(IntentClassifier intentClassifier, ILogger<AgentRouterService> logger)
    {
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AgentRoutingDecision RouteQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        var (intent, confidence) = _intentClassifier.ClassifyQuery(query);

        var targetAgent = MapIntentToAgent(intent);
        var routingConfidence = CalculateRoutingConfidence(confidence, intent);

        _logger.LogInformation(
            "[Router] Query classified: intent={Intent}, confidence={Confidence:F3}, targetAgent={Agent}",
            intent,
            confidence,
            targetAgent);

        return new AgentRoutingDecision(
            TargetAgent: targetAgent,
            Intent: intent,
            Confidence: routingConfidence,
            ShouldRoute: routingConfidence >= HighConfidenceThreshold,
            RequiresConfirmation: routingConfidence >= MediumConfidenceThreshold && routingConfidence < HighConfidenceThreshold,
            FallbackAgents: routingConfidence < MediumConfidenceThreshold ? GetFallbackAgents(intent) : null
        );
    }

    private static string MapIntentToAgent(AgentIntent intent)
    {
        return intent switch
        {
            AgentIntent.Tutorial => "TutorAgent",
            AgentIntent.RulesQuestion => "TutorAgent",
            AgentIntent.MoveValidation => "ArbitroAgent",
            AgentIntent.StrategicAnalysis => "DecisoreAgent",
            _ => "TutorAgent" // Default fallback
        };
    }

    private static double CalculateRoutingConfidence(double classificationConfidence, AgentIntent intent)
    {
        // Boost confidence for high-clarity intents
        var boost = intent switch
        {
            AgentIntent.MoveValidation => 0.05,  // "validate move" is very clear
            AgentIntent.StrategicAnalysis => 0.05, // "suggest move" is very clear
            AgentIntent.Tutorial => 0.03,
            _ => 0.0
        };

        return Math.Min(classificationConfidence + boost, 1.0);
    }

    private static List<string> GetFallbackAgents(AgentIntent intent)
    {
        // Return multiple agent suggestions for ambiguous queries
        return intent switch
        {
            AgentIntent.Unknown => new List<string> { "TutorAgent", "ArbitroAgent" },
            _ => new List<string> { "TutorAgent" }
        };
    }
}

/// <summary>
/// Routing decision with confidence and fallback options.
/// </summary>
internal sealed record AgentRoutingDecision(
    string TargetAgent,
    AgentIntent Intent,
    double Confidence,
    bool ShouldRoute,
    bool RequiresConfirmation,
    List<string>? FallbackAgents
);
