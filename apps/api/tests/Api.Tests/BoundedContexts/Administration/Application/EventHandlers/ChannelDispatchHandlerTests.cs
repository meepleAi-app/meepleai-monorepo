using Api.BoundedContexts.Administration.Application.EventHandlers;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="ChannelDispatchHandler"/> — Issue #1840 SP5 F4-C7.
///
/// <para>Covers the resilience contract:
/// <list type="bullet">
///   <item>IsDryRun=true → zero transport calls</item>
///   <item>Unknown channel name is logged + skipped (does not throw)</item>
///   <item>Missing/disabled channel config → skip silently</item>
///   <item>Slack failure does not abort Email sibling</item>
///   <item>Malformed JSON config → skip + log</item>
/// </list>
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public sealed class ChannelDispatchHandlerTests
{
    private readonly Mock<IAlertChannelRepository> _channelRepo = new();
    private readonly Mock<ISlackWebhookClient> _slackClient = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<ChannelDispatchHandler>> _logger = new();

    private ChannelDispatchHandler CreateHandler() =>
        new(_channelRepo.Object, _slackClient.Object, _emailService.Object, _logger.Object);

    private static AlertFiredEvent CreateEvent(
        bool isDryRun = false,
        bool isTest = false,
        params string[] channels) =>
        new(
            RuleId: Guid.NewGuid(),
            RuleName: "high_error_rate",
            AlertType: "HighErrorRate",
            Metric: "meepleai_api_error_rate",
            Value: 0.07,
            Threshold: 0.05,
            ThresholdUnit: "%",
            Severity: AlertSeverityKind.Critical,
            Channels: channels.Length == 0 ? new[] { "slack" } : channels,
            IsDryRun: isDryRun,
            IsTest: isTest,
            TriggeredBy: "admin@meepleai.dev");

    private static AlertChannel CreateSlackChannel(string webhookUrl = "https://hooks.slack.com/services/T/B/x") =>
        AlertChannel.Create(
            AlertChannelType.Slack,
            $$"""{"webhookUrl":"{{webhookUrl}}","channel":"#alerts"}""",
            isEnabled: true,
            createdBy: "admin");

    private static AlertChannel CreateEmailChannel(params string[] recipients)
    {
        var list = recipients.Length == 0 ? new[] { "ops@meepleai.dev" } : recipients;
        var json = "{\"recipients\":[" + string.Join(",", list.Select(r => $"\"{r}\"")) + "]}";
        return AlertChannel.Create(AlertChannelType.Email, json, true, "admin");
    }

    [Fact]
    public async Task Handle_WhenIsDryRunTrue_NeverCallsTransportClients()
    {
        var handler = CreateHandler();
        var evt = CreateEvent(true, true, "slack", "email");

        await handler.Handle(evt, CancellationToken.None);

        _slackClient.Verify(c => c.SendAsync(
            It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()),
            Times.Never, "DRY RUN must NEVER invoke Slack");
        _emailService.Verify(e => e.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "DRY RUN must NEVER invoke Email");
        _channelRepo.Verify(r => r.GetByTypeAsync(
            It.IsAny<AlertChannelType>(), It.IsAny<CancellationToken>()),
            Times.Never, "DRY RUN must skip repo lookup entirely");
    }

    [Fact]
    public async Task Handle_WithSlackChannelConfigured_PostsToWebhook()
    {
        var channel = CreateSlackChannel();
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);
        _slackClient
            .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlackSendResult(true, null, 200));

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"slack");

        await handler.Handle(evt, CancellationToken.None);

        _slackClient.Verify(c => c.SendAsync(
            "https://hooks.slack.com/services/T/B/x",
            It.IsAny<SlackMessage>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenChannelMissing_SkipsWithoutError()
    {
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertChannel?)null);

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"slack");

        var act = () => handler.Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _slackClient.Verify(c => c.SendAsync(
            It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenChannelDisabled_SkipsSilently()
    {
        var channel = AlertChannel.Create(
            AlertChannelType.Slack,
            """{"webhookUrl":"https://hooks.slack.com/x","channel":"#alerts"}""",
            isEnabled: false,
            createdBy: "admin");
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"slack");

        await handler.Handle(evt, CancellationToken.None);

        _slackClient.Verify(c => c.SendAsync(
            It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithSlackAndEmailChannels_BothDispatched()
    {
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSlackChannel());
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmailChannel());
        _slackClient
            .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlackSendResult(true));

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"slack", "email");

        await handler.Handle(evt, CancellationToken.None);

        _slackClient.Verify(c => c.SendAsync(
            It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _emailService.Verify(e => e.SendRawEmailAsync(
            "ops@meepleai.dev", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSlackFails_EmailStillDispatched()
    {
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSlackChannel());
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmailChannel());
        _slackClient
            .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network unreachable"));

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"slack", "email");

        await handler.Handle(evt, CancellationToken.None);

        // Email must still be dispatched even though Slack threw.
        _emailService.Verify(e => e.SendRawEmailAsync(
            "ops@meepleai.dev", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSlackConfigIsMalformed_SkipsGracefully()
    {
        var channel = AlertChannel.Create(
            AlertChannelType.Slack,
            """{"channel":"#alerts"}""", // missing webhookUrl
            true,
            "admin");
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"slack");

        await handler.Handle(evt, CancellationToken.None);

        _slackClient.Verify(c => c.SendAsync(
            It.IsAny<string>(), It.IsAny<SlackMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUnknownChannelName_SkipsWithoutThrowing()
    {
        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"pagerduty"); // explicitly out of #1840 scope

        var act = () => handler.Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _channelRepo.Verify(r => r.GetByTypeAsync(It.IsAny<AlertChannelType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithMultipleEmailRecipients_SendsToEach()
    {
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmailChannel("ops@meepleai.dev", "oncall@meepleai.dev"));

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"email");

        await handler.Handle(evt, CancellationToken.None);

        _emailService.Verify(e => e.SendRawEmailAsync(
            "ops@meepleai.dev", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _emailService.Verify(e => e.SendRawEmailAsync(
            "oncall@meepleai.dev", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOneRecipientFails_OtherRecipientStillReceives()
    {
        _channelRepo
            .Setup(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmailChannel("flaky@meepleai.dev", "stable@meepleai.dev"));
        _emailService
            .Setup(e => e.SendRawEmailAsync("flaky@meepleai.dev", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp 5xx"));

        var handler = CreateHandler();
        var evt = CreateEvent(false, false,"email");

        await handler.Handle(evt, CancellationToken.None);

        _emailService.Verify(e => e.SendRawEmailAsync(
            "stable@meepleai.dev", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
