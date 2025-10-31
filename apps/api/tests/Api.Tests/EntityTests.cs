using System;
using Api.Infrastructure.Entities;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

public class EntityTests
{
    private readonly ITestOutputHelper _output;

    [Fact]
    public void AgentEntity_HasEmptyChatsCollectionByDefault()
    {
        var agent = new AgentEntity();

        agent.Chats.Should().NotBeNull();
        agent.Chats.Should().BeEmpty();
    }

    [Fact]
    public void GameEntity_InitializesNavigationCollections()
    {
        var game = new GameEntity
        {
            Id = "game-1",
            Name = "Test Game"
        };

        game.RuleSpecs.Should().NotBeNull();
        game.Agents.Should().NotBeNull();
        game.Chats.Should().NotBeNull();
        Assert.Equal("game-1", game.Id);
        Assert.Equal("Test Game", game.Name);
        Assert.True(game.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void ChatEntity_AssignsIdentifiersAndTimestamps()
    {
        var chat = new ChatEntity
        {
            AgentId = "agent-1",
            GameId = "game-1"
        };

        Assert.NotEqual(Guid.Empty, chat.Id);
        Assert.Equal("agent-1", chat.AgentId);
        Assert.Equal("game-1", chat.GameId);
        Assert.True(chat.StartedAt <= DateTime.UtcNow);
        chat.Logs.Should().NotBeNull();
        chat.Logs.Should().BeEmpty();
    }

    [Fact]
    public void PdfDocumentEntity_UsesDefaultsForProcessingFields()
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

        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal("pending", document.ProcessingStatus);
        document.ExtractedText.Should().BeNull();
        document.ExtractedTables.Should().BeNull();
        document.Metadata.Should().BeNull();
    }

    [Fact]
    public void AuditLogEntity_DefaultsToGlobalContext()
    {
        var log = new AuditLogEntity
        {
            UserId = "user-1",
            Action = "Test",
            Resource = "Game",
            ResourceId = "game-1",
            Result = "Success",
            Details = "Performed action"
        };

        Assert.False(string.IsNullOrWhiteSpace(log.Id));
        Assert.Equal("user-1", log.UserId);
        Assert.Equal("Test", log.Action);
        Assert.Equal("Game", log.Resource);
        Assert.Equal("game-1", log.ResourceId);
        Assert.Equal("Success", log.Result);
        Assert.Equal("Performed action", log.Details);
        Assert.True(log.CreatedAt <= DateTime.UtcNow);
        log.IpAddress.Should().BeNull();
        log.UserAgent.Should().BeNull();
    }
}
