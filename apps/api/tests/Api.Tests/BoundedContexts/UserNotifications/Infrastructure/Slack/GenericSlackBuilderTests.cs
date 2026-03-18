using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class GenericSlackBuilderTests
{
    private readonly GenericSlackBuilder _sut;

    public GenericSlackBuilderTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://meepleai.app"
            })
            .Build();

        _sut = new GenericSlackBuilder(config);
    }

    [Fact]
    public void CanHandle_AlwaysReturnsFalse()
    {
        // The generic builder is the fallback — never selected via CanHandle
        _sut.CanHandle(NotificationType.NewComment).Should().BeFalse();
        _sut.CanHandle(NotificationType.ShareRequestCreated).Should().BeFalse();
    }

    [Fact]
    public void BuildMessage_WithGenericPayload_ProducesHeaderAndSectionBlocks()
    {
        // Arrange
        var payload = new GenericPayload("Test Title", "Test body text");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var blocks = doc.RootElement.GetProperty("blocks");

        // Assert
        blocks.GetArrayLength().Should().Be(2);

        var header = blocks[0];
        header.GetProperty("type").GetString().Should().Be("header");
        header.GetProperty("text").GetProperty("text").GetString().Should().Be("Test Title");

        var section = blocks[1];
        section.GetProperty("type").GetString().Should().Be("section");
        section.GetProperty("text").GetProperty("text").GetString().Should().Be("Test body text");
    }

    [Fact]
    public void BuildMessage_WithDeepLinkPath_AddsActionsBlockWithButton()
    {
        // Arrange
        var payload = new GenericPayload("Title", "Body");

        // Act
        var result = _sut.BuildMessage(payload, "/notifications/123");
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var blocks = doc.RootElement.GetProperty("blocks");

        // Assert
        blocks.GetArrayLength().Should().Be(3);

        var actions = blocks[2];
        actions.GetProperty("type").GetString().Should().Be("actions");

        var button = actions.GetProperty("elements")[0];
        button.GetProperty("type").GetString().Should().Be("button");
        button.GetProperty("action_id").GetString().Should().Be("open_meepleai");
        button.GetProperty("url").GetString().Should().Be("https://meepleai.app/notifications/123");
    }

    [Fact]
    public void BuildMessage_WithNullDeepLink_DoesNotAddActionsBlock()
    {
        // Arrange
        var payload = new GenericPayload("Title", "Body");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var blocks = doc.RootElement.GetProperty("blocks");

        // Assert
        blocks.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void BuildMessage_WithNonGenericPayload_UsesFallbackTitleAndBody()
    {
        // Arrange — passing a BadgePayload instead of GenericPayload
        var payload = new BadgePayload(Guid.NewGuid(), "Explorer", "You explored 10 games");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var blocks = doc.RootElement.GetProperty("blocks");

        // Assert — should use default fallback strings
        var header = blocks[0];
        header.GetProperty("text").GetProperty("text").GetString().Should().Be("MeepleAI Notification");

        var section = blocks[1];
        section.GetProperty("text").GetProperty("text").GetString().Should().Be("You have a new notification.");
    }
}
