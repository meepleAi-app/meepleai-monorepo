using Api.Infrastructure.Entities;
using Xunit;

namespace Api.Tests;

public class EntityTests
{
    [Fact]
    public void AgentEntity_InitializesCollections()
    {
        var agent = new AgentEntity();
        Assert.NotNull(agent.Chats);
    }

    [Fact]
    public void GameEntity_AllowsBasicPropertyAssignment()
    {
        var game = new GameEntity
        {
            Id = "game-1",
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal("game-1", game.Id);
        Assert.Equal("Test Game", game.Name);
        Assert.NotEqual(default, game.CreatedAt);
    }

    [Fact]
    public void ChatEntity_AssociatesWithAgentAndGame()
    {
        var chat = new ChatEntity
        {
            AgentId = "agent-1",
            GameId = "game-1"
        };

        Assert.Equal("agent-1", chat.AgentId);
        Assert.Equal("game-1", chat.GameId);
    }

    [Fact]
    public void PdfDocumentEntity_StoresMetadata()
    {
        var document = new PdfDocumentEntity
        {
            Id = "doc-1",
            GameId = "game-1",
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = "user-1"
        };

        Assert.Equal("doc-1", document.Id);
        Assert.Equal("game-1", document.GameId);
        Assert.Equal("rules.pdf", document.FileName);
    }

    [Fact]
    public void AuditLogEntity_CapturesContext()
    {
        var log = new AuditLogEntity
        {
            UserId = "user-1",
            Action = "Test",
            Resource = "Game",
            ResourceId = "game-1",
            Result = "Success"
        };

        Assert.Equal("user-1", log.UserId);
        Assert.Equal("Test", log.Action);
        Assert.Equal("Game", log.Resource);
        Assert.Equal("game-1", log.ResourceId);
        Assert.Equal("Success", log.Result);
    }
}
