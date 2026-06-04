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

        // Refetch after upsert returns a fresh aggregate w/ RowVersion populated
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
                updatedBy: "admin",
                rowVersion: new byte[] { 1, 2, 3 }));

        var cmd = new UpsertAlertChannelCommand(
            Type: "slack",
            ConfigJson: """{"webhookUrl":"https://hooks.slack.com/X"}""",
            IsEnabled: true,
            RowVersion: null,
            UpdatedBy: "admin");

        var handler = CreateHandler();
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be("slack");
        result.RowVersion.Should().NotBeNullOrEmpty();
        capture.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OnDbConcurrencyException_ThrowsConflictException()
    {
        var existing = AlertChannel.Reconstitute(
            AlertChannelType.Slack,
            """{"webhookUrl":"https://hooks.slack.com/v1"}""",
            true, null, null, null,
            DateTime.UtcNow, DateTime.UtcNow, "admin",
            rowVersion: new byte[] { 9, 9, 9 });
        _repo.Setup(r => r.GetByTypeAsync(AlertChannelType.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AlertChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("simulated"));

        var cmd = new UpsertAlertChannelCommand(
            "slack", """{"webhookUrl":"https://hooks.slack.com/v2"}""",
            true,
            RowVersion: Convert.ToBase64String(new byte[] { 1, 1, 1 }), // STALE
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
                DateTime.UtcNow, DateTime.UtcNow, "admin",
                new byte[] { 7 }));

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
