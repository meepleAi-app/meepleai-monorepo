using Api.BoundedContexts.Administration.Application.Commands.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands.AlertChannels;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public sealed class UpsertAlertChannelCommandHandlerTests
{
    private readonly Mock<IAlertChannelRepository> _repo = new();

    private UpsertAlertChannelCommandHandler CreateHandler() => new(_repo.Object);

    [Fact]
    public async Task Handle_WhenChannelMissing_CreatesNewAggregate()
    {
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertChannel?)null);

        var capture = new List<AlertChannel>();
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .Callback<AlertChannel, CancellationToken>((c, _) => capture.Add(c))
            .Returns(Task.CompletedTask);

        // Refetch after upsert returns a fresh aggregate w/ xmin populated
        _repo.SetupSequence(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertChannel?)null) // initial lookup
            .ReturnsAsync(AlertChannel.Reconstitute(
                AlertChannelType.Slack,
                """{"webhookUrl":"https://hooks.slack.com/X"}""",
                isEnabled: true,
                lastTestedAt: null,
                lastTestStatus: null,
                lastTestMessage: null,
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow,
                createdBy: "admin",
                updatedBy: "admin",
                xmin: 3u));

        var cmd = new UpsertAlertChannelCommand(
            Type: "slack",
            ConfigJson: """{"webhookUrl":"https://hooks.slack.com/X"}""",
            IsEnabled: true,
            Xmin: null,
            UpdatedBy: "admin");

        var handler = CreateHandler();
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be("slack");
        result.Xmin.Should().NotBe(0u);
        capture.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OnDbConcurrencyException_ThrowsConflictException()
    {
        var existing = AlertChannel.Reconstitute(
            AlertChannelType.Slack,
            """{"webhookUrl":"https://hooks.slack.com/v1"}""",
            true, null, null, null,
            DateTime.UtcNow, DateTime.UtcNow, "admin", "admin",
            xmin: 9u);
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("simulated"));

        var cmd = new UpsertAlertChannelCommand(
            "slack", """{"webhookUrl":"https://hooks.slack.com/v2"}""",
            true,
            Xmin: 1u, // STALE
            UpdatedBy: "admin");

        var handler = CreateHandler();
        var act = () => handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*modified by another admin*");
    }

    [Fact]
    public async Task Handle_OnFirstCreate_PersistsConfigJsonAndIsEnabled()
    {
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertChannel?)null);

        AlertChannel? captured = null;
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .Callback<AlertChannel, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);

        _repo.SetupSequence(r => r.GetByTypeAsync(AlertChannelType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertChannel?)null)
            .ReturnsAsync(AlertChannel.Reconstitute(
                AlertChannelType.Email,
                """{"recipients":["ops@meepleai.dev"]}""",
                false, null, null, null,
                DateTime.UtcNow, DateTime.UtcNow, "admin", "admin",
                7u));

        var cmd = new UpsertAlertChannelCommand(
            "email", """{"recipients":["ops@meepleai.dev"]}""", false, null, "admin");
        var handler = CreateHandler();

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be("email");
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(AlertChannelType.Email);
        captured.IsEnabled.Should().BeFalse();
        captured.ConfigJson.Should().Contain("ops@meepleai.dev");
    }
}
