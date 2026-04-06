using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class AdminAlertSlackBuilderTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);

    private static AdminAlertSlackBuilder CreateSut()
        => new(new FakeTimeProvider(FixedTime));

    [Theory]
    [InlineData("admin_new_share_request")]
    [InlineData("admin_stale_share_requests")]
    [InlineData("admin_review_lock_expiring")]
    [InlineData("admin_system_health_alert")]
    [InlineData("admin_model_status_changed")]
    [InlineData("admin_openrouter_threshold_alert")]
    [InlineData("admin_openrouter_daily_summary")]
    [InlineData("admin_shared_game_submitted")]
    public void CanHandle_AdminTypes_ReturnsTrue(string typeValue)
    {
        CreateSut().CanHandle(NotificationType.FromString(typeValue)).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_NonAdminType_ReturnsFalse()
    {
        CreateSut().CanHandle(NotificationType.BadgeEarned).Should().BeFalse();
    }

    [Fact]
    public void BuildMessage_ContainsFixedTimestampInContextBlock()
    {
        var payload = new GenericPayload("Test Alert", "Something happened.");

        var result = CreateSut().BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        // AdminAlertSlackBuilder uses attachments
        var blocks = doc.RootElement.GetProperty("attachments")[0].GetProperty("blocks");
        var contextText = blocks[2].GetProperty("elements")[0].GetProperty("text").GetString();

        contextText.Should().Contain("2026-03-15 10:30");
    }

    [Fact]
    public void BuildMessage_CriticalTitle_ProducesDangerAttachment()
    {
        var payload = new GenericPayload("CRITICAL Circuit Breaker Open", "Service degraded.");

        var result = CreateSut().BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("attachments")[0].GetProperty("color").GetString()
            .Should().Be("danger");
    }

    [Fact]
    public void BuildMessage_WarningTitle_ProducesWarningAttachment()
    {
        var payload = new GenericPayload("WARNING Budget RPM approaching", "80% of quota used.");

        var result = CreateSut().BuildMessage(payload, null);
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("attachments")[0].GetProperty("color").GetString()
            .Should().Be("warning");
    }

    [Fact]
    public void BuildMessage_WithWrongPayloadType_ThrowsArgumentException()
    {
        var act = () => CreateSut().BuildMessage(new BadgePayload(Guid.NewGuid(), "X", "Y"), null);
        act.Should().Throw<ArgumentException>().WithMessage("*GenericPayload*BadgePayload*");
    }
}
