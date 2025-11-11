using System;
using Api.Infrastructure.Entities;
using Xunit;
using FluentAssertions;
using Xunit;

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
            Id = Guid.NewGuid(),
            Name = "Test Game"
        };

        game.RuleSpecs.Should().NotBeNull();
        game.Agents.Should().NotBeNull();
        game.Chats.Should().NotBeNull();
        game.Id.Should().Be("game-1");
        game.Name.Should().Be("Test Game");
        (game.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void ChatEntity_AssignsIdentifiersAndTimestamps()
    {
        var chat = new ChatEntity
        {
            AgentId = Guid.NewGuid(),
            GameId = Guid.NewGuid()
        };

        chat.Id.Should().NotBe(Guid.Empty);
        chat.AgentId.Should().Be("agent-1");
        chat.GameId.Should().Be("game-1");
        (chat.StartedAt <= DateTime.UtcNow).Should().BeTrue();
        chat.Logs.Should().NotBeNull();
        chat.Logs.Should().BeEmpty();
    }

    [Fact]
    public void PdfDocumentEntity_UsesDefaultsForProcessingFields()
    {
        var document = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid()
        };

        document.ContentType.Should().Be("application/pdf");
        document.ProcessingStatus.Should().Be("pending");
        document.ExtractedText.Should().BeNull();
        document.ExtractedTables.Should().BeNull();
        document.Metadata.Should().BeNull();
    }

    [Fact]
    public void AuditLogEntity_DefaultsToGlobalContext()
    {
        var log = new AuditLogEntity
        {
            UserId = Guid.NewGuid(),
            Action = "Test",
            Resource = "Game",
            ResourceId = "game-1",
            Result = "Success",
            Details = "Performed action"
        };

        string.IsNullOrWhiteSpace(log.Id).Should().BeFalse();
        log.UserId.Should().Be("user-1");
        log.Action.Should().Be("Test");
        log.Resource.Should().Be("Game");
        log.ResourceId.Should().Be("game-1");
        log.Result.Should().Be("Success");
        log.Details.Should().Be("Performed action");
        (log.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
        log.IpAddress.Should().BeNull();
        log.UserAgent.Should().BeNull();
    }
}
