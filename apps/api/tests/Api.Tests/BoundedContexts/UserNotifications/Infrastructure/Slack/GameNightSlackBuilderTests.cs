using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class GameNightSlackBuilderTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly GameNightSlackBuilder _sut;

    public GameNightSlackBuilderTests()
    {
        var timeProvider = new FakeTimeProvider(FixedTime);
        _sut = new GameNightSlackBuilder(timeProvider);
    }

    [Theory]
    [InlineData("game_night_invitation")]
    [InlineData("game_night_rsvp_received")]
    [InlineData("game_night_reminder_24h")]
    [InlineData("game_night_reminder_1h")]
    [InlineData("game_night_cancelled")]
    public void CanHandle_GameNightTypes_ReturnsTrue(string typeValue)
    {
        _sut.CanHandle(NotificationType.FromString(typeValue)).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_NonGameNightType_ReturnsFalse()
    {
        _sut.CanHandle(NotificationType.ShareRequestCreated).Should().BeFalse();
    }

    [Fact]
    public void BuildMessage_ProducesBlockIdWithGnPrefix()
    {
        // Arrange
        var gnId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var payload = new GameNightPayload(gnId, "Friday Night Games", DateTime.UtcNow, "Alice");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var actionsBlock = doc.RootElement.GetProperty("blocks")[2];
        var blockId = actionsBlock.GetProperty("block_id").GetString();

        // Assert
        blockId.Should().NotBeNull();
        var segments = blockId!.Split(':');
        segments.Should().HaveCount(3);
        segments[0].Should().Be("gn");
        segments[1].Should().Be(gnId.ToString());
        long.TryParse(segments[2], out var ts).Should().BeTrue();
        ts.Should().Be(FixedTime.ToUnixTimeSeconds());
    }

    [Fact]
    public void BuildMessage_ProducesRsvpButtons()
    {
        // Arrange
        var gnId = Guid.NewGuid();
        var payload = new GameNightPayload(gnId, "Board Game Night", DateTime.UtcNow, "Bob");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var elements = doc.RootElement.GetProperty("blocks")[2].GetProperty("elements");

        // Assert
        elements.GetArrayLength().Should().Be(3);

        elements[0].GetProperty("action_id").GetString().Should().Be("game_night_rsvp_yes");
        elements[0].GetProperty("value").GetString().Should().Be(gnId.ToString());
        elements[0].GetProperty("style").GetString().Should().Be("primary");

        elements[1].GetProperty("action_id").GetString().Should().Be("game_night_rsvp_no");
        elements[1].GetProperty("value").GetString().Should().Be(gnId.ToString());
        elements[1].GetProperty("style").GetString().Should().Be("danger");

        elements[2].GetProperty("action_id").GetString().Should().Be("game_night_rsvp_maybe");
        elements[2].GetProperty("value").GetString().Should().Be(gnId.ToString());
    }

    [Fact]
    public void BuildMessage_IncludesDateAndOrganizerInSection()
    {
        // Arrange
        var scheduledAt = new DateTime(2026, 4, 10, 19, 30, 0, DateTimeKind.Utc);
        var payload = new GameNightPayload(Guid.NewGuid(), "Friday Games", scheduledAt, "Mario Rossi");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var sectionText = doc.RootElement.GetProperty("blocks")[1]
            .GetProperty("text").GetProperty("text").GetString();

        // Assert
        sectionText.Should().Contain("Mario Rossi");
        sectionText.Should().Contain("2026");
    }

    [Fact]
    public void BuildMessage_HeaderContainsGameNightTitle()
    {
        // Arrange
        var payload = new GameNightPayload(Guid.NewGuid(), "Epic Board Game Night", DateTime.UtcNow, "Host");

        // Act
        var result = _sut.BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var headerText = doc.RootElement.GetProperty("blocks")[0]
            .GetProperty("text").GetProperty("text").GetString();

        // Assert
        headerText.Should().Contain("Epic Board Game Night");
    }

    [Fact]
    public void BuildMessage_WithWrongPayloadType_ThrowsArgumentException()
    {
        var payload = new GenericPayload("Title", "Body");
        var act = () => _sut.BuildMessage(payload, null);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*GameNightPayload*GenericPayload*");
    }
}
