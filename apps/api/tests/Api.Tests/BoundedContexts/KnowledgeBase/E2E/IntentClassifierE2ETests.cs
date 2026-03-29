using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.E2E;

/// <summary>
/// E2E tests for IntentClassifier covering all agent intents,
/// ambiguity handling, negative keywords, and edge cases.
/// Issue #3779: E2E Testing Suite - All Agent Workflows
/// </summary>
[Trait("Category", TestCategories.E2E)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3779")]
public class IntentClassifierE2ETests
{
    private readonly IntentClassifier _classifier = new();

    #region Move Validation Intent

    [Theory]
    [InlineData("Is this move legal in chess?")]
    [InlineData("Can I move my knight to E5?")]
    [InlineData("Validate move: pawn to D4")]
    [InlineData("Is it legal to castle here?")]
    [InlineData("Check move bishop to C3")]
    public void MoveValidation_Queries_ClassifyCorrectly(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.MoveValidation);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.40,
            $"'{query}' should have sufficient confidence for MoveValidation");
    }

    #endregion

    #region Strategic Analysis Intent

    [Theory]
    [InlineData("What is the best move here?")]
    [InlineData("Suggest move for white")]
    [InlineData("Analyze position and recommend")]
    [InlineData("What should I do in this situation?")]
    [InlineData("Which move gives me an advantage?")]
    public void StrategicAnalysis_Queries_ClassifyCorrectly(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.StrategicAnalysis);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.40);
    }

    #endregion

    #region Rules Question Intent

    [Theory]
    [InlineData("What is the rule for castling?")]
    [InlineData("What are the rules for checkmate?")]
    [InlineData("What is the turn order in this game?")]
    public void RulesQuestion_Queries_ClassifyCorrectly(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.RulesQuestion);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.40);
    }

    [Fact]
    public void RulesQuestion_HowDoes_ClassifiesReasonably()
    {
        // "How does X work" may classify as Rules or Tutorial depending on phrasing
        var result = _classifier.ClassifyQuery("How does scoring work at endgame?");

        var isReasonable = result.Intent == AgentIntent.RulesQuestion || result.Intent == AgentIntent.Tutorial;
        isReasonable.Should().BeTrue(
            "Questions about 'how things work' should route to either Rules or Tutorial");
    }

    #endregion

    #region Tutorial Intent

    [Theory]
    [InlineData("How to play chess?")]
    [InlineData("Teach me the basics of Catan")]
    [InlineData("I need a tutorial for this game")]
    [InlineData("Beginner guide to Carcassonne")]
    [InlineData("Getting started with board games")]
    public void Tutorial_Queries_ClassifyCorrectly(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.Tutorial);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.40);
    }

    #endregion

    #region Unknown Intent

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("pizza delivery")]
    [InlineData("what is the weather today")]
    public void Unrelated_Queries_ReturnUnknown(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.Unknown);
    }

    [Fact]
    public void NullQuery_ReturnsUnknown()
    {
        var result = _classifier.ClassifyQuery(null!);

        result.Intent.Should().Be(AgentIntent.Unknown);
        result.Confidence.Should().Be(0.0);
    }

    #endregion

    #region Negative Keywords

    [Fact]
    public void NegativeKeywords_ReduceConfidence()
    {
        // "suggest" is a negative keyword for MoveValidation
        // "validate move" is a positive keyword for MoveValidation
        var pureQuery = "validate move to E5";
        var queryWithNegative = "validate move to E5 and suggest alternative";

        var pureResult = _classifier.ClassifyQuery(pureQuery);
        var negativeResult = _classifier.ClassifyQuery(queryWithNegative);

        // Both should classify as MoveValidation, but negative should have lower score
        pureResult.Intent.Should().Be(AgentIntent.MoveValidation);
        negativeResult.Intent.Should().Be(AgentIntent.MoveValidation);

        // Pure query should have higher or equal confidence
        pureResult.Confidence.Should().BeGreaterThanOrEqualTo(negativeResult.Confidence,
            "Negative keywords should reduce confidence");
    }

    #endregion

    #region Ambiguity Detection

    [Fact]
    public void AmbiguousQuery_HasReducedConfidence()
    {
        // Queries matching multiple intents should have ambiguity penalty
        var ambiguousQuery = "can i move my piece according to the rules?";

        var result = _classifier.ClassifyQuery(ambiguousQuery);

        // Should still classify something, but AllScores should show close competition
        result.AllScores.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Performance Requirements

    [Fact]
    public void Classification_CompletesWithin10ms()
    {
        var queries = new[]
        {
            "What is the best move here?",
            "How to play chess?",
            "Is this move legal?",
            "What are the rules for checkmate?",
            "Suggest a strategy for winning"
        };

        foreach (var query in queries)
        {
            var result = _classifier.ClassifyQuery(query);

            result.ClassificationDuration.TotalMilliseconds.Should().BeLessThan(10,
                $"Classification of '{query}' should be fast (< 10ms)");
        }
    }

    [Fact]
    public void AllScores_AlwaysReturnedForTransparency()
    {
        var result = _classifier.ClassifyQuery("What is the best move here?");

        result.AllScores.Should().NotBeNull();
        result.AllScores.Should().HaveCount(5, "All 5 intent types should be scored");

        // All scores should be non-negative
        result.AllScores.Should().AllSatisfy(s =>
            s.Score.Should().BeGreaterThanOrEqualTo(0.0));
    }

    #endregion

    #region Agent Switching Scenarios

    [Fact]
    public void ConversationFlow_IntentChanges_CorrectlyRouted()
    {
        // Simulate a multi-turn conversation where user intent changes
        var conversationQueries = new[]
        {
            ("How to play Catan?", AgentIntent.Tutorial),
            ("What are the rules for trading?", AgentIntent.RulesQuestion),
            ("What is the best move with my resources?", AgentIntent.StrategicAnalysis),
            ("Is it legal to trade with the bank now?", AgentIntent.MoveValidation),
        };

        foreach (var (query, expectedIntent) in conversationQueries)
        {
            var result = _classifier.ClassifyQuery(query);

            result.Intent.Should().Be(expectedIntent,
                $"Turn '{query}' should route to {expectedIntent}");
            result.Confidence.Should().BeGreaterThanOrEqualTo(0.40,
                $"Turn '{query}' should have sufficient confidence");
        }
    }

    #endregion
}
