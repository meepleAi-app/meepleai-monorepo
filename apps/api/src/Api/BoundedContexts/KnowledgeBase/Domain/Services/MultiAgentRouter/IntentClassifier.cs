using System.Diagnostics;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Classifies user query intent for multi-agent routing using weighted keyword scoring.
/// Issue #4336: Multi-Agent Router - Intent Classification.
/// </summary>
/// <remarks>
/// Uses a weighted keyword matching approach where each intent has:
/// - Primary keywords (high weight): strong indicators of intent
/// - Secondary keywords (medium weight): supporting indicators
/// - Negative keywords (penalty): indicators the query is NOT this intent
/// Final confidence = normalized weighted sum, clamped to [0, 1].
/// </remarks>
internal sealed class IntentClassifier
{
    private static readonly IntentPattern[] IntentPatterns =
    [
        new(AgentIntent.MoveValidation, new WeightedKeyword[]
        {
            // Primary - strong move validation signals
            new("validate move", 1.0), new("is this move legal", 1.0), new("is this move valid", 1.0),
            new("can i move", 0.9), new("allowed to move", 0.9), new("legal move", 0.9),
            new("is it legal", 0.85), new("validate", 0.8), new("check move", 0.85),
            // Secondary - supporting context
            new("move my", 0.5), new("place my", 0.5), new("play this", 0.4),
        },
        // Negative keywords - these suggest it's NOT move validation
        ["suggest", "best", "strategy", "teach", "learn", "explain"]),

        new(AgentIntent.StrategicAnalysis, new WeightedKeyword[]
        {
            // Primary
            new("suggest move", 1.0), new("best move", 1.0), new("what should i do", 0.95),
            new("analyze position", 0.95), new("recommend", 0.9), new("optimal", 0.9),
            new("evaluate position", 0.9), new("which move", 0.85),
            // Secondary
            new("strategy", 0.7), new("advantage", 0.6), new("winning", 0.5),
            new("tactics", 0.6), new("better move", 0.8), new("improve position", 0.8),
        },
        ["validate", "legal", "rule", "learn", "tutorial", "teach"]),

        new(AgentIntent.RulesQuestion, new WeightedKeyword[]
        {
            // Primary
            new("what is the rule", 1.0), new("rules for", 0.95), new("game rules", 0.95),
            new("how does this work", 0.85), new("how do i", 0.8), new("turn order", 0.9),
            new("allowed to", 0.75), new("can i", 0.6),
            // Secondary
            new("rule", 0.7), new("setup", 0.65), new("phase", 0.6),
            new("scoring", 0.6), new("endgame", 0.5), new("component", 0.5),
            new("mechanism", 0.5), new("when can", 0.6),
        },
        ["suggest", "best", "optimal", "strategy", "validate move"]),

        new(AgentIntent.Tutorial, new WeightedKeyword[]
        {
            // Primary
            new("how to play", 1.0), new("teach me", 1.0), new("tutorial", 0.95),
            new("learn to play", 0.95), new("explain how", 0.9), new("beginner guide", 0.95),
            new("getting started", 0.9), new("introduction to", 0.85),
            // Secondary
            new("learn", 0.7), new("explain", 0.6), new("understand", 0.55),
            new("new to", 0.7), new("first time", 0.7), new("walkthrough", 0.8),
        },
        ["validate", "legal move", "best move", "analyze position"]),
    ];

    /// <summary>
    /// Classifies the query intent and returns the best match with confidence.
    /// </summary>
    public IntentClassificationResult ClassifyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new IntentClassificationResult(AgentIntent.Unknown, 0.0, TimeSpan.Zero, []);

        var stopwatch = Stopwatch.StartNew();
        var lowerQuery = query.ToLowerInvariant();

        var scores = new List<IntentScore>(IntentPatterns.Length);

        foreach (var pattern in IntentPatterns)
        {
            var score = CalculateIntentScore(lowerQuery, pattern);
            scores.Add(new IntentScore(pattern.Intent, score));
        }

        stopwatch.Stop();

        // Sort by score descending
        scores.Sort((a, b) => b.Score.CompareTo(a.Score));

        var bestMatch = scores[0];

        // If best score is too low, return Unknown
        if (bestMatch.Score < 0.40)
        {
            return new IntentClassificationResult(
                AgentIntent.Unknown,
                bestMatch.Score,
                stopwatch.Elapsed,
                scores);
        }

        // Check if the top two scores are too close (ambiguous)
        if (scores.Count > 1)
        {
            var secondBest = scores[1];
            var gap = bestMatch.Score - secondBest.Score;

            // If gap < 0.10, reduce confidence to signal ambiguity
            if (gap < 0.10 && secondBest.Score > 0.40)
            {
                var ambiguityPenalty = (0.10 - gap) * 2.0; // max penalty 0.20
                var adjustedConfidence = Math.Max(bestMatch.Score - ambiguityPenalty, 0.50);

                return new IntentClassificationResult(
                    bestMatch.Intent,
                    adjustedConfidence,
                    stopwatch.Elapsed,
                    scores);
            }
        }

        return new IntentClassificationResult(
            bestMatch.Intent,
            bestMatch.Score,
            stopwatch.Elapsed,
            scores);
    }

    private static double CalculateIntentScore(string query, IntentPattern pattern)
    {
        double totalWeight = 0.0;
        int matchCount = 0;

        foreach (var keyword in pattern.Keywords)
        {
            if (query.Contains(keyword.Phrase, StringComparison.Ordinal))
            {
                totalWeight += keyword.Weight;
                matchCount++;
            }
        }

        if (matchCount == 0)
            return 0.0;

        // Apply negative keyword penalty
        int negativeCount = 0;
        foreach (var negative in pattern.NegativeKeywords)
        {
            if (query.Contains(negative, StringComparison.Ordinal))
                negativeCount++;
        }

        var negativePenalty = negativeCount * 0.15;

        // Normalize: use average weight of matched keywords, boosted by match count
        var averageWeight = totalWeight / matchCount;
        var matchBonus = Math.Min(matchCount * 0.05, 0.15); // up to 0.15 bonus for multiple matches

        var score = Math.Min(averageWeight + matchBonus - negativePenalty, 1.0);
        return Math.Max(score, 0.0);
    }
}

/// <summary>
/// Agent intent types for routing decisions.
/// </summary>
internal enum AgentIntent
{
    Unknown,
    Tutorial,
    RulesQuestion,
    MoveValidation,
    StrategicAnalysis
}

/// <summary>
/// Result of intent classification including all scored intents for transparency.
/// </summary>
internal sealed record IntentClassificationResult(
    AgentIntent Intent,
    double Confidence,
    TimeSpan ClassificationDuration,
    List<IntentScore> AllScores);

/// <summary>
/// Score for a single intent.
/// </summary>
internal sealed record IntentScore(AgentIntent Intent, double Score);

/// <summary>
/// Keyword with associated weight for intent matching.
/// </summary>
internal sealed record WeightedKeyword(string Phrase, double Weight);

/// <summary>
/// Pattern definition for an intent with weighted keywords and negative keywords.
/// </summary>
internal sealed record IntentPattern(
    AgentIntent Intent,
    WeightedKeyword[] Keywords,
    string[] NegativeKeywords);
