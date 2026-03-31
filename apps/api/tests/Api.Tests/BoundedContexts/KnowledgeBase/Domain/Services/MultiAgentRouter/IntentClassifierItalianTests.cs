using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Tests for Italian keyword support in IntentClassifier.
/// Verifies that Italian-speaking users are correctly routed to specialized agents.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class IntentClassifierItalianTests
{
    private readonly IntentClassifier _sut = new();

    #region MoveValidation - Italian

    [Theory]
    [InlineData("posso muovere il cavallo?")]
    [InlineData("questa mossa è legale?")]
    [InlineData("è valida questa mossa?")]
    [InlineData("è legale piazzare qui?")]
    [InlineData("verificare la mossa del pedone")]
    [InlineData("controllare la mossa")]
    public void ClassifyQuery_ItalianMoveValidation_ReturnsCorrectIntent(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.MoveValidation);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.70,
            $"query '{query}' should have confidence >= 0.70, got {result.Confidence:F3}");
    }

    [Theory]
    [InlineData("è valida questa mossa?")]
    [InlineData("questa mossa è legale?")]
    public void ClassifyQuery_ClearItalianMoveValidation_HighConfidence(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.MoveValidation);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.85,
            $"clear Italian move validation query should have high confidence, got {result.Confidence:F3}");
    }

    #endregion

    #region StrategicAnalysis - Italian

    [Theory]
    [InlineData("qual è la mossa migliore?")]
    [InlineData("cosa mi conviene fare?")]
    [InlineData("suggeriscimi una mossa")]
    [InlineData("consigliami la strategia migliore")]
    [InlineData("cosa dovrei fare adesso?")]
    [InlineData("analizza posizione attuale")]
    public void ClassifyQuery_ItalianStrategic_ReturnsCorrectIntent(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.StrategicAnalysis);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.70,
            $"query '{query}' should have confidence >= 0.70, got {result.Confidence:F3}");
    }

    [Theory]
    [InlineData("qual è la mossa migliore?")]
    [InlineData("cosa dovrei fare adesso?")]
    public void ClassifyQuery_ClearItalianStrategic_HighConfidence(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.StrategicAnalysis);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.85,
            $"clear Italian strategic query should have high confidence, got {result.Confidence:F3}");
    }

    #endregion

    #region RulesQuestion - Italian

    [Theory]
    [InlineData("come funziona il turno?")]
    [InlineData("qual è la regola per il punteggio?")]
    [InlineData("regole del gioco")]
    [InlineData("regola per il movimento")]
    [InlineData("ordine dei turni")]
    [InlineData("come si calcola il punteggio?")]
    public void ClassifyQuery_ItalianRules_ReturnsCorrectIntent(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.RulesQuestion);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.50,
            $"query '{query}' should have confidence >= 0.50, got {result.Confidence:F3}");
    }

    [Theory]
    [InlineData("qual è la regola per il punteggio?")]
    [InlineData("regole del gioco")]
    public void ClassifyQuery_ClearItalianRules_HighConfidence(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.RulesQuestion);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.85,
            $"clear Italian rules query should have high confidence, got {result.Confidence:F3}");
    }

    #endregion

    #region Tutorial - Italian

    [Theory]
    [InlineData("come si gioca?")]
    [InlineData("insegnami a giocare")]
    [InlineData("spiegami le regole base")]
    [InlineData("guida per principianti")]
    [InlineData("introduzione al gioco")]
    [InlineData("imparare a giocare a Catan")]
    public void ClassifyQuery_ItalianTutorial_ReturnsCorrectIntent(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.Tutorial);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.70,
            $"query '{query}' should have confidence >= 0.70, got {result.Confidence:F3}");
    }

    [Theory]
    [InlineData("come si gioca?")]
    [InlineData("insegnami a giocare")]
    public void ClassifyQuery_ClearItalianTutorial_HighConfidence(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.Tutorial);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.85,
            $"clear Italian tutorial query should have high confidence, got {result.Confidence:F3}");
    }

    #endregion

    #region Narrative - Italian

    [Theory]
    [InlineData("racconta la storia del gioco")]
    [InlineData("narratore del gioco")]
    [InlineData("ambientazione del mondo")]
    public void ClassifyQuery_ItalianNarrative_ReturnsCorrectIntent(string query)
    {
        var result = _sut.ClassifyQuery(query);

        result.Intent.Should().Be(AgentIntent.Narrative);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.70,
            $"query '{query}' should have confidence >= 0.70, got {result.Confidence:F3}");
    }

    #endregion

    #region Cross-Intent Italian Disambiguation

    [Fact]
    public void ClassifyQuery_ItalianMoveVsStrategy_CorrectlyDisambiguates()
    {
        // "posso muovere" should be MoveValidation, not Strategic
        var moveResult = _sut.ClassifyQuery("posso muovere il cavallo?");
        moveResult.Intent.Should().Be(AgentIntent.MoveValidation);

        // "mossa migliore" should be Strategic, not MoveValidation
        var strategyResult = _sut.ClassifyQuery("qual è la mossa migliore?");
        strategyResult.Intent.Should().Be(AgentIntent.StrategicAnalysis);
    }

    [Fact]
    public void ClassifyQuery_ItalianTutorialVsRules_CorrectlyDisambiguates()
    {
        // "come si gioca" should be Tutorial
        var tutorialResult = _sut.ClassifyQuery("come si gioca a Catan?");
        tutorialResult.Intent.Should().Be(AgentIntent.Tutorial);

        // "regole del gioco" should be Rules
        var rulesResult = _sut.ClassifyQuery("regole del gioco");
        rulesResult.Intent.Should().Be(AgentIntent.RulesQuestion);
    }

    #endregion
}
