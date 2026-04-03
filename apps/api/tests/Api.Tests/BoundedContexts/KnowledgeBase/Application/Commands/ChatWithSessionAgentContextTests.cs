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
        var ctx = new GameSessionContext(
            GameId: Guid.NewGuid(),
            GameTitle: "Catan",
            Players: ["Marco", "Luca", "Sara (ospite)"],
            CurrentTurn: 3,
            ResponseLanguage: "it"
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
            new GameSessionContext(Guid.NewGuid(), "", ["Player1"], 1, "it"));
    }

    [Fact]
    public void GameSessionContext_DefaultLanguage_IsItalian()
    {
        var ctx = new GameSessionContext(Guid.NewGuid(), "Dixit", ["A", "B"], 1);
        Assert.Equal("it", ctx.ResponseLanguage);
    }
}
