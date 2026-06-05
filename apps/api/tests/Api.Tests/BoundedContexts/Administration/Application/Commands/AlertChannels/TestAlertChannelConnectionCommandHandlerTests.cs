using Api.BoundedContexts.Administration.Application.Commands.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands.AlertChannels;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public sealed class TestAlertChannelConnectionCommandHandlerTests
{
    private readonly Mock<IAlertChannelRepository> _repo = new();
    private readonly Mock<ISlackWebhookClient> _slack = new();

    private TestAlertChannelConnectionCommandHandler CreateHandler() =>
        new(_repo.Object, _slack.Object);

    private static AlertChannel SlackChannel(string url = "https://hooks.slack.com/services/T/B/x") =>
        AlertChannel.Create(
            AlertChannelType.Slack,
            $$"""{"webhookUrl":"{{url}}","channel":"#alerts"}""",
            true, "admin");

    private static AlertChannel EmailChannel(params string[] recipients)
    {
        var list = recipients.Length == 0 ? new[] { "ops@meepleai.dev" } : recipients;
        var json = "{\"recipients\":[" + string.Join(",", list.Select(r => $"\"{r}\"")) + "]}";
        return AlertChannel.Create(AlertChannelType.Email, json, true, "admin");
    }

    [Fact]
    public async Task Handle_WhenSlackProbeSucceeds_RecordsOkOnAggregate()
    {
        var channel = SlackChannel();
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);
        _slack.Setup(s => s.TestConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlackTestResult(true, "Connection OK", 200));

        AlertChannel? saved = null;
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .Callback<AlertChannel, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new TestAlertChannelConnectionCommand("slack"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Connection OK");
        result.StatusCode.Should().Be(200);
        saved.Should().NotBeNull();
        saved!.LastTestStatus.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WhenSlackProbeFails_RecordsErrorOnAggregate()
    {
        var channel = SlackChannel();
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);
        _slack.Setup(s => s.TestConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlackTestResult(false, "HTTP 401 invalid_token", 401));

        AlertChannel? saved = null;
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .Callback<AlertChannel, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new TestAlertChannelConnectionCommand("slack"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("HTTP 401 invalid_token");
        result.StatusCode.Should().Be(401);
        saved!.LastTestStatus.Should().Be("error");
    }

    [Fact]
    public async Task Handle_WhenChannelMissing_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertChannel?)null);

        var handler = CreateHandler();
        var act = () => handler.Handle(new TestAlertChannelConnectionCommand("slack"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_EmailWithRecipients_RecordsOkWithoutSmtpProbe()
    {
        var channel = EmailChannel("ops@meepleai.dev", "oncall@meepleai.dev");
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        AlertChannel? saved = null;
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .Callback<AlertChannel, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new TestAlertChannelConnectionCommand("email"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("2 recipient");
        saved!.LastTestStatus.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_EmailWithEmptyRecipients_RecordsError()
    {
        var channel = AlertChannel.Create(AlertChannelType.Email, """{"recipients":[]}""", true, "admin");
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        AlertChannel? saved = null;
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .Callback<AlertChannel, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new TestAlertChannelConnectionCommand("email"), CancellationToken.None);

        result.Success.Should().BeFalse();
        saved!.LastTestStatus.Should().Be("error");
    }

    [Fact]
    public async Task Handle_SlackWithoutWebhookUrl_RecordsError()
    {
        // Malformed config — missing webhookUrl key.
        var channel = AlertChannel.Create(AlertChannelType.Slack, """{"channel":"#alerts"}""", true, "admin");
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        AlertChannel? saved = null;
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .Callback<AlertChannel, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new TestAlertChannelConnectionCommand("slack"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("webhookUrl");
        saved!.LastTestStatus.Should().Be("error");
        _slack.Verify(s => s.TestConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
