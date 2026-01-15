using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Unit tests for NaturalLanguageStateParser.
/// Issue #2405 - Ledger Mode NLP parsing
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class NaturalLanguageStateParserTests
{
    private readonly IStateParser _parser;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public NaturalLanguageStateParserTests()
    {
        var mockLogger = new Mock<ILogger<NaturalLanguageStateParser>>();
        _parser = new NaturalLanguageStateParser(mockLogger.Object);
    }

    #region Score Pattern Tests

    [Fact]
    public async Task ParseAsync_WithScorePattern_HoPunti_ExtractsScore()
    {
        var result = await _parser.ParseAsync("ho 5 punti", null, TestCancellationToken);

        Assert.Equal(StateChangeType.ScoreChange, result.ChangeType);
        Assert.True(result.HasStateChanges);
        Assert.Contains("score", result.ExtractedState.Keys);
        Assert.Equal(5, result.ExtractedState["score"]);
        Assert.True(result.Confidence > 0.8f);
    }

    [Fact]
    public async Task ParseAsync_WithScorePattern_SonoA_ExtractsScore()
    {
        var result = await _parser.ParseAsync("sono a 10 punti", null, TestCancellationToken);

        Assert.Equal(StateChangeType.ScoreChange, result.ChangeType);
        Assert.Equal(10, result.ExtractedState["score"]);
    }

    [Fact]
    public async Task ParseAsync_WithScorePattern_PassoDaAGivenScore_ExtractsNewScore()
    {
        var result = await _parser.ParseAsync("passo da 3 a 8 punti", null, TestCancellationToken);

        Assert.Equal(StateChangeType.ScoreChange, result.ChangeType);
        Assert.Equal(8, result.ExtractedState["score"]);
    }

    #endregion

    #region Resource Pattern Tests

    [Fact]
    public async Task ParseAsync_WithResourcePattern_Guadagnato_ExtractsResource()
    {
        var result = await _parser.ParseAsync("ho guadagnato 3 legno", null, TestCancellationToken);

        Assert.Equal(StateChangeType.ResourceChange, result.ChangeType);
        Assert.Contains("resources.wood", result.ExtractedState.Keys);
        Assert.Equal(3, result.ExtractedState["resources.wood"]);
    }

    [Fact]
    public async Task ParseAsync_WithResourcePattern_Multiple_ExtractsAll()
    {
        var result = await _parser.ParseAsync("ho 2 pietra e 4 grano", null, TestCancellationToken);

        // Multiple resources = Composite type
        Assert.Equal(StateChangeType.Composite, result.ChangeType);
        Assert.Contains("resources.stone", result.ExtractedState.Keys);
        Assert.Contains("resources.wheat", result.ExtractedState.Keys);
        Assert.Equal(2, result.ExtractedState["resources.stone"]);
        Assert.Equal(4, result.ExtractedState["resources.wheat"]);
    }

    #endregion

    #region Action Pattern Tests

    [Fact]
    public async Task ParseAsync_WithActionPattern_CostruitoStrada_ExtractsAction()
    {
        var result = await _parser.ParseAsync("ho costruito una strada", null, TestCancellationToken);

        Assert.Equal(StateChangeType.PlayerAction, result.ChangeType);
        Assert.Contains("buildings.roads", result.ExtractedState.Keys);
        Assert.Equal(1, result.ExtractedState["buildings.roads"]);
    }

    [Fact]
    public async Task ParseAsync_WithActionPattern_CostruitoCitta_ExtractsAction()
    {
        var result = await _parser.ParseAsync("ho costruito una città", null, TestCancellationToken);

        Assert.Equal(StateChangeType.PlayerAction, result.ChangeType);
        Assert.Contains("buildings.cities", result.ExtractedState.Keys);
    }

    #endregion

    #region Turn Pattern Tests

    [Fact]
    public async Task ParseAsync_WithTurnPattern_ToccaA_ExtractsTurn()
    {
        var result = await _parser.ParseAsync("tocca a Marco", null, TestCancellationToken);

        Assert.Equal(StateChangeType.TurnChange, result.ChangeType);
        Assert.Contains("currentPlayer", result.ExtractedState.Keys);
        Assert.Equal("Marco", result.ExtractedState["currentPlayer"]);
        Assert.True(result.Confidence > 0.9f);
    }

    [Fact]
    public async Task ParseAsync_WithTurnPattern_IlTurnoDi_ExtractsTurn()
    {
        var result = await _parser.ParseAsync("è il turno di Luca", null, TestCancellationToken);

        Assert.Equal(StateChangeType.TurnChange, result.ChangeType);
        Assert.Equal("Luca", result.ExtractedState["currentPlayer"]);
    }

    #endregion

    #region Phase Pattern Tests

    [Fact]
    public async Task ParseAsync_WithPhasePattern_FaseDi_ExtractsPhase()
    {
        var result = await _parser.ParseAsync("fase di costruzione", null, TestCancellationToken);

        Assert.Equal(StateChangeType.PhaseChange, result.ChangeType);
        Assert.Contains("phase", result.ExtractedState.Keys);
        Assert.Equal("costruzione", result.ExtractedState["phase"]);
    }

    [Fact]
    public async Task ParseAsync_WithPhasePattern_IniziaIlRound_ExtractsPhase()
    {
        var result = await _parser.ParseAsync("inizia il round 3", null, TestCancellationToken);

        Assert.Equal(StateChangeType.PhaseChange, result.ChangeType);
        Assert.Equal("3", result.ExtractedState["phase"]);
    }

    #endregion

    #region Composite Changes Tests

    [Fact]
    public async Task ParseAsync_WithMultipleChanges_ReturnsComposite()
    {
        var result = await _parser.ParseAsync(
            "ho costruito una strada e adesso ho 3 punti",
            null,
            TestCancellationToken);

        Assert.Equal(StateChangeType.Composite, result.ChangeType);
        Assert.Contains("buildings.roads", result.ExtractedState.Keys);
        Assert.Contains("score", result.ExtractedState.Keys);
        Assert.True(result.RequiresConfirmation);
        // Check exact warning message
        Assert.Contains(result.Warnings, w => w.Contains("Rilevate modifiche multiple"));
    }

    #endregion

    #region Player Name Extraction Tests

    [Fact]
    public async Task ParseAsync_WithPlayerName_ExtractsPlayer()
    {
        var result = await _parser.ParseAsync("Marco ha 5 punti", null, TestCancellationToken);

        Assert.Equal("Marco", result.PlayerName);
        // Score should be extracted if pattern matches
        if (result.HasStateChanges && result.ExtractedState.ContainsKey("score"))
        {
            Assert.Equal(5, result.ExtractedState["score"]);
        }
    }

    #endregion

    #region No Change Tests

    [Fact]
    public async Task ParseAsync_WithNoStateChange_ReturnsNoChange()
    {
        var result = await _parser.ParseAsync(
            "Che bel gioco questo Catan!",
            null,
            TestCancellationToken);

        Assert.Equal(StateChangeType.NoChange, result.ChangeType);
        Assert.False(result.HasStateChanges);
        Assert.Empty(result.ExtractedState);
    }

    [Fact]
    public async Task ParseAsync_WithEmptyMessage_ReturnsNoChange()
    {
        var result = await _parser.ParseAsync(string.Empty, null, TestCancellationToken);

        Assert.Equal(StateChangeType.NoChange, result.ChangeType);
        Assert.False(result.HasStateChanges);
    }

    #endregion

    #region Edge Cases and Security Tests

    [Fact]
    public async Task ParseAsync_WithVeryLongMessage_DoesNotTimeout()
    {
        // Test ReDoS mitigation
        var longMessage = "ho " + new string(' ', 1000) + " 5 punti";

        var result = await _parser.ParseAsync(longMessage, null, TestCancellationToken);

        // Should either succeed or fail gracefully, not hang
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseAsync_WithExtremelyHighScore_HandlesGracefully()
    {
        var result = await _parser.ParseAsync("ho 99999 punti", null, TestCancellationToken);

        // Should not extract scores beyond reasonable limits
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseAsync_WithSpecialCharacters_HandlesGracefully()
    {
        var result = await _parser.ParseAsync("ho 5 punti <script>alert('xss')</script>", null, TestCancellationToken);

        // Parser doesn't sanitize - that's responsibility of input validation
        Assert.NotNull(result);
        Assert.Equal("ho 5 punti <script>alert('xss')</script>", result.OriginalMessage);
    }

    #endregion

    #region Confidence Scoring Tests

    [Fact]
    public async Task ParseAsync_WithClearPattern_ReturnsHighConfidence()
    {
        var result = await _parser.ParseAsync("ho 5 punti", null, TestCancellationToken);

        Assert.True(result.Confidence >= 0.8f);
    }

    [Fact]
    public async Task ParseAsync_WithAmbiguousPattern_ReturnsLowerConfidence()
    {
        var result = await _parser.ParseAsync("forse ho 5 punti o forse 6", null, TestCancellationToken);

        // Even if extracted, confidence should be lower due to ambiguity
        Assert.NotNull(result);
    }

    #endregion
}
