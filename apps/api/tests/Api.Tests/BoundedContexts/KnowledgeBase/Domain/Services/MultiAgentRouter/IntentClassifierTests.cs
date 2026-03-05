using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Tests for IntentClassifier - weighted keyword-based intent classification.
/// Issue #4336: Multi-Agent Router - Intent Classification.
/// Target: >95% routing accuracy for clear intent queries.
/// </summary>
public class IntentClassifierTests
{
    private readonly IntentClassifier _classifier = new();

    #region MoveValidation Intent

    [Theory]
    [InlineData("validate move e2 to e4")]
    [InlineData("is this move legal?")]
    [InlineData("is this move valid in Catan?")]
    [InlineData("can i move my knight to e5?")]
    [InlineData("is it legal to place a settlement here?")]
    [InlineData("check move validity")]
    public void ClassifyQuery_MoveValidationQueries_ReturnsMoveValidation(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.MoveValidation, result.Intent);
        Assert.True(result.Confidence >= 0.70,
            $"Expected confidence >= 0.70, got {result.Confidence:F3} for query: '{query}'");
    }

    [Theory]
    [InlineData("validate move e2 to e4")]
    [InlineData("is this move legal?")]
    [InlineData("is this move valid?")]
    public void ClassifyQuery_ClearMoveValidation_HighConfidence(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.MoveValidation, result.Intent);
        Assert.True(result.Confidence >= 0.85,
            $"Expected high confidence >= 0.85 for clear query, got {result.Confidence:F3}");
    }

    #endregion

    #region StrategicAnalysis Intent

    [Theory]
    [InlineData("suggest move for my rook")]
    [InlineData("best move in this position")]
    [InlineData("what should i do next?")]
    [InlineData("analyze position and recommend")]
    [InlineData("which move gives me an advantage?")]
    [InlineData("evaluate position for optimal play")]
    [InlineData("what's the better move here?")]
    public void ClassifyQuery_StrategicAnalysisQueries_ReturnsStrategicAnalysis(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.StrategicAnalysis, result.Intent);
        Assert.True(result.Confidence >= 0.70,
            $"Expected confidence >= 0.70, got {result.Confidence:F3} for query: '{query}'");
    }

    [Theory]
    [InlineData("suggest move for my knight")]
    [InlineData("best move in this situation")]
    public void ClassifyQuery_ClearStrategicAnalysis_HighConfidence(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.StrategicAnalysis, result.Intent);
        Assert.True(result.Confidence >= 0.85,
            $"Expected high confidence >= 0.85 for clear query, got {result.Confidence:F3}");
    }

    #endregion

    #region RulesQuestion Intent

    [Theory]
    [InlineData("what is the rule for castling?")]
    [InlineData("rules for Catan trading")]
    [InlineData("game rules for setup phase")]
    [InlineData("what is the turn order in this game?")]
    public void ClassifyQuery_RulesQuestionQueries_ReturnsRulesQuestion(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.RulesQuestion, result.Intent);
        Assert.True(result.Confidence >= 0.60,
            $"Expected confidence >= 0.60, got {result.Confidence:F3} for query: '{query}'");
    }

    #endregion

    #region Tutorial Intent

    [Theory]
    [InlineData("how to play Catan")]
    [InlineData("teach me chess basics")]
    [InlineData("tutorial for beginners")]
    [InlineData("learn to play Ticket to Ride")]
    [InlineData("explain how this game works")]
    [InlineData("getting started with Terraforming Mars")]
    [InlineData("beginner guide for Wingspan")]
    public void ClassifyQuery_TutorialQueries_ReturnsTutorial(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.Tutorial, result.Intent);
        Assert.True(result.Confidence >= 0.70,
            $"Expected confidence >= 0.70, got {result.Confidence:F3} for query: '{query}'");
    }

    [Theory]
    [InlineData("how to play chess")]
    [InlineData("teach me the game")]
    public void ClassifyQuery_ClearTutorial_HighConfidence(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.Tutorial, result.Intent);
        Assert.True(result.Confidence >= 0.85,
            $"Expected high confidence >= 0.85 for clear query, got {result.Confidence:F3}");
    }

    #endregion

    #region Unknown / Ambiguous Intent

    [Theory]
    [InlineData("hello")]
    [InlineData("what time is it?")]
    [InlineData("who are you?")]
    [InlineData("thank you")]
    public void ClassifyQuery_UnrelatedQueries_ReturnsUnknown(string query)
    {
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(AgentIntent.Unknown, result.Intent);
    }

    [Fact]
    public void ClassifyQuery_EmptyQuery_ReturnsUnknown()
    {
        var result = _classifier.ClassifyQuery("");

        Assert.Equal(AgentIntent.Unknown, result.Intent);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void ClassifyQuery_NullQuery_ReturnsUnknown()
    {
        var result = _classifier.ClassifyQuery(null!);

        Assert.Equal(AgentIntent.Unknown, result.Intent);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void ClassifyQuery_WhitespaceQuery_ReturnsUnknown()
    {
        var result = _classifier.ClassifyQuery("   ");

        Assert.Equal(AgentIntent.Unknown, result.Intent);
    }

    #endregion

    #region Negative Keyword Penalty

    [Fact]
    public void ClassifyQuery_MoveValidationWithStrategyKeywords_ReducedConfidence()
    {
        // "validate move" has high weight, "suggest" is a negative keyword for MoveValidation
        var withNegative = _classifier.ClassifyQuery("validate move but suggest alternatives");
        var without = _classifier.ClassifyQuery("validate move to e4");

        // MoveValidation score with negative keyword should be lower
        var withNegativeScore = withNegative.AllScores
            .First(s => s.Intent == AgentIntent.MoveValidation).Score;
        var withoutScore = without.AllScores
            .First(s => s.Intent == AgentIntent.MoveValidation).Score;

        Assert.True(withoutScore > withNegativeScore,
            $"Expected lower MoveValidation score with negative keywords. Without: {withoutScore:F3}, With: {withNegativeScore:F3}");
    }

    #endregion

    #region Classification Duration

    [Fact]
    public void ClassifyQuery_PerformanceRequirement_CompletesUnder50ms()
    {
        var result = _classifier.ClassifyQuery("validate move e2 to e4");

        Assert.True(result.ClassificationDuration.TotalMilliseconds < 50,
            $"Classification took {result.ClassificationDuration.TotalMilliseconds:F1}ms, expected <50ms");
    }

    #endregion

    #region All Scores Transparency

    [Fact]
    public void ClassifyQuery_ReturnsAllIntentScores()
    {
        var result = _classifier.ClassifyQuery("validate move");

        Assert.NotEmpty(result.AllScores);
        Assert.Equal(5, result.AllScores.Count); // 5 intent patterns
    }

    [Fact]
    public void ClassifyQuery_AllScoresSortedDescending()
    {
        var result = _classifier.ClassifyQuery("suggest the best move");

        for (int i = 0; i < result.AllScores.Count - 1; i++)
        {
            Assert.True(result.AllScores[i].Score >= result.AllScores[i + 1].Score,
                $"Scores not sorted: [{i}]={result.AllScores[i].Score:F3} < [{i + 1}]={result.AllScores[i + 1].Score:F3}");
        }
    }

    #endregion

    #region Routing Accuracy (>95% target)

    [Theory]
    [InlineData("validate my move to d4", "MoveValidation")]
    [InlineData("is this move legal in chess?", "MoveValidation")]
    [InlineData("can i move my pawn two squares?", "MoveValidation")]
    [InlineData("suggest the best move for white", "StrategicAnalysis")]
    [InlineData("what should i do in this position?", "StrategicAnalysis")]
    [InlineData("analyze position and find optimal play", "StrategicAnalysis")]
    [InlineData("what are the rules for en passant?", "RulesQuestion")]
    [InlineData("how does the turn order work?", "RulesQuestion")]
    [InlineData("game rules for the setup phase", "RulesQuestion")]
    [InlineData("teach me how to play chess", "Tutorial")]
    [InlineData("tutorial for Catan beginners", "Tutorial")]
    [InlineData("learn to play Ticket to Ride", "Tutorial")]
    [InlineData("how to play Wingspan", "Tutorial")]
    [InlineData("explain how castling works", "Tutorial")]
    [InlineData("getting started with Go", "Tutorial")]
    [InlineData("beginner guide for Azul", "Tutorial")]
    [InlineData("which move is better, e4 or d4?", "StrategicAnalysis")]
    [InlineData("evaluate position for tactics", "StrategicAnalysis")]
    [InlineData("what is the rule for promotion?", "RulesQuestion")]
    [InlineData("rules for placing workers in Agricola", "RulesQuestion")]
    public void ClassifyQuery_ClearIntentQueries_CorrectClassification(string query, string expectedIntentName)
    {
        var expectedIntent = Enum.Parse<AgentIntent>(expectedIntentName);
        var result = _classifier.ClassifyQuery(query);

        Assert.Equal(expectedIntent, result.Intent);
    }

    #endregion
}
