using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class ShareRequestSlackBuilderTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ShareRequestSlackBuilder _sut;

    public ShareRequestSlackBuilderTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://meepleai.app"
            })
            .Build();

        var timeProvider = new FakeTimeProvider(FixedTime);
        _sut = new ShareRequestSlackBuilder(config, timeProvider);
    }

    [Theory]
    [InlineData("share_request_created")]
    [InlineData("share_request_approved")]
    [InlineData("share_request_rejected")]
    public void CanHandle_ShareRequestTypes_ReturnsTrue(string typeValue)
    {
        _sut.CanHandle(NotificationType.FromString(typeValue)).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_NonShareRequestType_ReturnsFalse()
    {
        _sut.CanHandle(NotificationType.BadgeEarned).Should().BeFalse();
    }

    [Fact]
    public void BuildMessage_ProducesCorrectBlockIdFormat()
    {
        // Arrange
        var shareRequestId = Guid.Parse("abc00000-1111-2222-3333-444444444444");
        var payload = new ShareRequestPayload(shareRequestId, "Mario Rossi", "Catan", null);

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var blocks = doc.RootElement.GetProperty("blocks");

        // Assert — actions block has correct block_id
        var actionsBlock = blocks[2];
        var blockId = actionsBlock.GetProperty("block_id").GetString();
        blockId.Should().NotBeNull();

        var segments = blockId!.Split(':');
        segments.Should().HaveCount(3);
        segments[0].Should().Be("sr");
        segments[1].Should().Be(shareRequestId.ToString());
        long.TryParse(segments[2], out var ts).Should().BeTrue();
        ts.Should().Be(FixedTime.ToUnixTimeSeconds());
    }

    [Fact]
    public void BuildMessage_ProducesApproveRejectOpenButtons()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payload = new ShareRequestPayload(id, "Alice", "Wingspan", null);

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var elements = doc.RootElement.GetProperty("blocks")[2].GetProperty("elements");

        // Assert
        elements.GetArrayLength().Should().Be(3);

        elements[0].GetProperty("action_id").GetString().Should().Be("share_request_approve");
        elements[0].GetProperty("value").GetString().Should().Be(id.ToString());
        elements[0].GetProperty("style").GetString().Should().Be("primary");

        elements[1].GetProperty("action_id").GetString().Should().Be("share_request_reject");
        elements[1].GetProperty("value").GetString().Should().Be(id.ToString());
        elements[1].GetProperty("style").GetString().Should().Be("danger");

        elements[2].GetProperty("action_id").GetString().Should().Be("open_meepleai");
        elements[2].GetProperty("url").GetString().Should().Contain($"/share-requests/{id}");
    }

    [Fact]
    public void BuildMessage_WithGameImageUrl_AddsImageAccessory()
    {
        // Arrange
        var payload = new ShareRequestPayload(
            Guid.NewGuid(), "Mario", "Catan", "https://example.com/catan.jpg");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var section = doc.RootElement.GetProperty("blocks")[1];

        // Assert
        section.GetProperty("accessory").GetProperty("type").GetString().Should().Be("image");
        section.GetProperty("accessory").GetProperty("image_url").GetString()
            .Should().Be("https://example.com/catan.jpg");
        section.GetProperty("accessory").GetProperty("alt_text").GetString().Should().Be("Catan");
    }

    [Fact]
    public void BuildMessage_WithoutGameImageUrl_NoAccessory()
    {
        // Arrange
        var payload = new ShareRequestPayload(Guid.NewGuid(), "Mario", "Catan", null);

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var section = doc.RootElement.GetProperty("blocks")[1];

        // Assert
        section.TryGetProperty("accessory", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildMessage_ButtonValuesContainOnlyGuid()
    {
        // Arrange — security requirement: action values contain only the GUID
        var id = Guid.NewGuid();
        var payload = new ShareRequestPayload(id, "User", "Game", "https://img.com/game.jpg");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var elements = doc.RootElement.GetProperty("blocks")[2].GetProperty("elements");

        // Assert
        for (int i = 0; i < 2; i++) // Approve and Reject buttons
        {
            var value = elements[i].GetProperty("value").GetString();
            Guid.TryParse(value, out _).Should().BeTrue($"button {i} value should be a valid GUID");
        }
    }

    [Fact]
    public void BuildMessage_WithWrongPayloadType_ThrowsArgumentException()
    {
        // Arrange
        var payload = new GenericPayload("Title", "Body");

        // Act
        var act = () => _sut.BuildMessage(payload, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ShareRequestPayload*GenericPayload*");
    }
}
