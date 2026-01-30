using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Tests for AgentPromptBuilder domain service.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class AgentPromptBuilderTests
{
    private readonly AgentPromptBuilder _builder;

    public AgentPromptBuilderTests()
    {
        _builder = new AgentPromptBuilder();
    }

    [Fact]
    public void BuildSessionPrompt_WithFullGameState_IncludesAllContext()
    {
        // Arrange
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var gameState = GameState.Create(
            currentTurn: 5,
            activePlayer: aliceId,
            playerScores: new Dictionary<Guid, int> { { aliceId, 10 }, { bobId, 8 } },
            gamePhase: "MainPhase",
            lastAction: "Played card");

        // Act
        var prompt = _builder.BuildSessionPrompt(
            "Strategy Expert",
            "Catan",
            gameState,
            "What should I do next?");

        // Assert
        Assert.Contains("You are a Strategy Expert for Catan", prompt);
        Assert.Contains("Turn: 5", prompt);
        Assert.Contains($"Active player ID: {aliceId}", prompt);
        Assert.Contains($"Player {aliceId}: 10", prompt);
        Assert.Contains($"Player {bobId}: 8", prompt);
        Assert.Contains("Phase: MainPhase", prompt);
        Assert.Contains("Last action: Played card", prompt);
        Assert.Contains("User question: What should I do next?", prompt);
    }

    [Fact]
    public void BuildSessionPrompt_WithMinimalGameState_OmitsOptionalFields()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var gameState = GameState.Initial(playerId);

        // Act
        var prompt = _builder.BuildSessionPrompt(
            "Rules Expert",
            "Chess",
            gameState,
            "Can I castle?");

        // Assert
        Assert.Contains("You are a Rules Expert for Chess", prompt);
        Assert.Contains("Turn: 1", prompt); // Initial() sets turn to 1
        Assert.Contains($"Active player ID: {playerId}", prompt);
        Assert.Contains("User question: Can I castle?", prompt);
        Assert.DoesNotContain("Scores:", prompt); // Empty scores should be omitted
    }

    [Fact]
    public void BuildSessionPrompt_WithNullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var gameState = GameState.Initial(Guid.NewGuid());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _builder.BuildSessionPrompt(null!, "Game", gameState, "Question"));

        Assert.Throws<ArgumentNullException>(() =>
            _builder.BuildSessionPrompt("Expert", null!, gameState, "Question"));

        Assert.Throws<ArgumentNullException>(() =>
            _builder.BuildSessionPrompt("Expert", "Game", null!, "Question"));

        Assert.Throws<ArgumentNullException>(() =>
            _builder.BuildSessionPrompt("Expert", "Game", gameState, null!));
    }
}
