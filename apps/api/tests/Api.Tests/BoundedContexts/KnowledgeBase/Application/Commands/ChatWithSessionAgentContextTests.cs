using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Unit tests for GameSessionContext — verifies system prompt enrichment
/// and validation logic for contextual Italian game responses.
/// </summary>
public class ChatWithSessionAgentContextTests
{
    [Fact]
    public void GameSessionContext_BuildsSystemPromptEnrichment_WithAllPlayers()
    {
        var ctx = GameSessionContext.Create(
            gameId: Guid.NewGuid(),
            gameTitle: "Catan",
            players: ["Marco", "Luca", "Sara (ospite)"],
            currentTurn: 3,
            responseLanguage: "it"
        );

        var prompt = ctx.ToSystemPromptEnrichment();

        Assert.Contains("Catan", prompt);
        Assert.Contains("Marco", prompt);
        Assert.Contains("Sara (ospite)", prompt);
        Assert.Contains("Turno corrente: 3", prompt);
        Assert.Contains("italiano", prompt);
    }

    [Fact]
    public void GameSessionContext_WithMissingGameTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            GameSessionContext.Create(Guid.NewGuid(), "", ["Player1"], 1, "it"));
    }

    [Fact]
    public void GameSessionContext_DefaultLanguage_IsItalian()
    {
        var ctx = GameSessionContext.Create(Guid.NewGuid(), "Dixit", ["A", "B"], 1);
        Assert.Equal("it", ctx.ResponseLanguage);
    }
}
