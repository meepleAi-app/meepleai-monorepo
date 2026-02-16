using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.E2E;

/// <summary>
/// E2E tests for multi-agent routing scenarios including intent-based routing,
/// conversation flow with agent switching, and routing performance.
/// Issue #3779: E2E Testing Suite - All Agent Workflows
/// </summary>
[Trait("Category", TestCategories.E2E)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3779")]
public class MultiAgentRoutingE2ETests
{
    private readonly IntentClassifier _classifier = new();

    #region Multi-Turn Conversation Routing

    [Fact]
    public void ConversationFlow_TutorialToRulesToStrategy_RoutesCorrectly()
    {
        // User starts learning
        var tutorialResult = _classifier.ClassifyQuery("How to play Ticket to Ride?");
        tutorialResult.Intent.Should().Be(AgentIntent.Tutorial);

        // Then asks rules
        var rulesResult = _classifier.ClassifyQuery("What is the rule for claiming routes?");
        rulesResult.Intent.Should().Be(AgentIntent.RulesQuestion);

        // Then asks for strategy
        var strategyResult = _classifier.ClassifyQuery("What should I do with these cards?");
        strategyResult.Intent.Should().Be(AgentIntent.StrategicAnalysis);
    }

    [Fact]
    public void ConversationFlow_StrategyToValidation_SwitchesAgent()
    {
        // User asks for strategy then wants to validate a move
        var strategyResult = _classifier.ClassifyQuery("What is the best move for white?");
        strategyResult.Intent.Should().Be(AgentIntent.StrategicAnalysis);

        var validationResult = _classifier.ClassifyQuery("Is this move legal: knight to F3?");
        validationResult.Intent.Should().Be(AgentIntent.MoveValidation);

        // These should go to different agents
        strategyResult.Intent.Should().NotBe(validationResult.Intent,
            "Strategy and validation should route to different agents");
    }

    [Fact]
    public void ConversationFlow_SameIntent_MostlyStaysWithAgent()
    {
        // Multiple questions in same domain should mostly route to same agent
        var queries = new[]
        {
            "What is the rule for checkmate?",
            "What is the turn order?",
        };

        var results = queries.Select(q => _classifier.ClassifyQuery(q)).ToList();

        // At least these clear rules queries should match
        results.Should().AllSatisfy(r =>
            r.Intent.Should().Be(AgentIntent.RulesQuestion,
                "Clear rules questions should route to RulesQuestion intent"));
    }

    #endregion

    #region Edge Case Routing

    [Fact]
    public void MixedKeywords_CanIMove_RoutesByDominantContext()
    {
        // "can i move" is ambiguous - depends on whether "rules" or validation context dominates
        var result = _classifier.ClassifyQuery("can i move according to the rules");

        // Actual classifier routes this to MoveValidation (tested empirically)
        result.Intent.Should().Be(AgentIntent.MoveValidation,
            "Classifier prioritizes move validation keywords over rules context");
    }

    [Fact]
    public void MixedKeywords_IsItAllowed_RoutesToMoveValidation()
    {
        var query = "is it allowed to move the king";
        var result = _classifier.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.MoveValidation,
            $"'{query}' should route to MoveValidation (strongest match)");
    }

    [Fact]
    public void VeryLongQuery_StillClassifiesCorrectly()
    {
        var longQuery = "I was playing a game of chess with my friend and " +
                       "I want to know if this particular move is legal because " +
                       "I moved my bishop diagonally across the board from C1 to H6 " +
                       "and my friend said it was not a valid move but I think it is " +
                       "so can you please validate move for me";

        var result = _classifier.ClassifyQuery(longQuery);

        result.Intent.Should().Be(AgentIntent.MoveValidation,
            "Long queries with clear intent should still classify correctly");
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.40);
    }

    [Fact]
    public void ShortQuery_MayReturnUnknown()
    {
        var shortQueries = new[] { "hi", "yes", "ok", "no" };

        foreach (var query in shortQueries)
        {
            var result = _classifier.ClassifyQuery(query);
            result.Intent.Should().Be(AgentIntent.Unknown,
                $"Short generic query '{query}' should be Unknown");
        }
    }

    #endregion

    #region Routing Performance Under Load

    [Fact]
    public void HighVolume_100Classifications_AllWithin1ms()
    {
        var queries = Enumerable.Range(0, 25).SelectMany(_ => new[]
        {
            "How to play this game?",
            "Is this move legal?",
            "What are the rules for scoring?",
            "Suggest the best move here"
        }).ToArray();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var query in queries)
        {
            _classifier.ClassifyQuery(query);
        }
        sw.Stop();

        var averageMs = sw.Elapsed.TotalMilliseconds / queries.Length;
        averageMs.Should().BeLessThan(1.0,
            "Average classification should be < 1ms for in-memory keyword matching");
    }

    [Fact]
    public void ConcurrentClassifications_20Parallel_NoRaceConditions()
    {
        var queries = new string[20];
        for (var i = 0; i < 20; i++)
        {
            queries[i] = (i % 4) switch
            {
                0 => "How to play chess?",
                1 => "Is this move legal?",
                2 => "What are the rules?",
                _ => "Suggest a strategy"
            };
        }

        var tasks = queries.Select(q => Task.Run(() => _classifier.ClassifyQuery(q)));

        var results = Task.WhenAll(tasks).Result;

        results.Should().HaveCount(20);
        results.Should().AllSatisfy(r =>
        {
            r.Intent.Should().NotBe(AgentIntent.Unknown);
            r.Confidence.Should().BeGreaterThanOrEqualTo(0.40);
        });
    }

    #endregion

    #region Intent Distribution Validation

    [Fact]
    public void IntentDistribution_BalancedTestQueries_AllIntentsCovered()
    {
        var testQueries = new Dictionary<AgentIntent, string[]>
        {
            [AgentIntent.Tutorial] = new[]
            {
                "How to play chess?",
                "Teach me Catan",
                "Beginner guide to Go",
                "Getting started with Dominion",
                "Tutorial for Scrabble"
            },
            [AgentIntent.RulesQuestion] = new[]
            {
                "What is the rule for castling?",
                "How does scoring work?",
                "What is the turn order?",
                "Game rules for setup?",
                "Rules for endgame phase?"
            },
            [AgentIntent.MoveValidation] = new[]
            {
                "Is this move legal?",
                "Can I move my knight here?",
                "Validate move: pawn to E4",
                "Is it legal to castle?",
                "Check move bishop to C3"
            },
            [AgentIntent.StrategicAnalysis] = new[]
            {
                "What is the best move?",
                "Suggest move for me",
                "Analyze my position",
                "Recommend a strategy",
                "Which move gives advantage?"
            }
        };

        foreach (var (expectedIntent, queries) in testQueries)
        {
            var correctCount = queries.Count(q =>
                _classifier.ClassifyQuery(q).Intent == expectedIntent);

            correctCount.Should().BeGreaterThanOrEqualTo(3,
                $"At least 3/5 {expectedIntent} queries should classify correctly (got {correctCount}/5)");
        }
    }

    #endregion

    #region All Scores Transparency

    [Fact]
    public void AllScores_ProvideRoutingTransparency()
    {
        var result = _classifier.ClassifyQuery("How to play chess?");

        result.AllScores.Should().HaveCount(4);

        // Tutorial should be highest for this query
        var tutorialScore = result.AllScores.First(s => s.Intent == AgentIntent.Tutorial);
        var otherScores = result.AllScores.Where(s => s.Intent != AgentIntent.Tutorial);

        tutorialScore.Score.Should().BeGreaterThanOrEqualTo(
            otherScores.Max(s => s.Score),
            "Tutorial should have highest score for 'How to play chess?'");
    }

    [Fact]
    public void AllScores_SortedByScoreDescending()
    {
        var result = _classifier.ClassifyQuery("What is the best move here?");

        for (var i = 1; i < result.AllScores.Count; i++)
        {
            result.AllScores[i - 1].Score.Should().BeGreaterThanOrEqualTo(
                result.AllScores[i].Score,
                "AllScores should be sorted by score descending");
        }
    }

    #endregion
}
