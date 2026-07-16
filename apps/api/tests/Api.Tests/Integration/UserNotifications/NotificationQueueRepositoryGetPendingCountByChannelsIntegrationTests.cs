using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.UserNotifications;

/// <summary>
/// Integration tests for <see cref="NotificationQueueRepository.GetPendingCountByChannelsAsync"/>
/// against a real Testcontainers PostgreSQL instance.
///
/// Guards the fix for the mis-attributed <c>/health</c> 503: the Slack queue health check must
/// count pending items scoped to the Slack channels only, so an unrelated backlog on another
/// channel (e.g. 310 pending email items on staging) does not trip the Slack check. The
/// unfiltered <see cref="NotificationQueueRepository.GetPendingCountAsync"/> is asserted alongside
/// to make the channel-leak contrast explicit.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationQueueRepositoryGetPendingCountByChannelsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private INotificationQueueRepository? _repository;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public NotificationQueueRepositoryGetPendingCountByChannelsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_notifqueuepending_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<INotificationQueueRepository, NotificationQueueRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<INotificationQueueRepository>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch (NpgsqlException)
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task GetPendingCountByChannelsAsync_SlackChannels_CountsOnlySlackPending_ExcludingOtherChannels()
    {
        // Arrange — a mixed queue mirroring the staging incident: a large email backlog
        // plus a handful of genuine Slack items and some non-eligible rows.
        var pastRetry = DateTime.UtcNow.AddMinutes(-5);   // failed + retry-due  → counts as pending
        var futureRetry = DateTime.UtcNow.AddMinutes(30); // failed + not yet due → excluded

        await SeedAsync(
            // email — must be excluded from the Slack-scoped count
            NewItem(NotificationChannelType.Email, status: "pending"),
            NewItem(NotificationChannelType.Email, status: "pending"),
            NewItem(NotificationChannelType.Email, status: "pending"),
            NewItem(NotificationChannelType.Email, status: "failed", nextRetryAt: pastRetry),
            // slack_user — included
            NewItem(NotificationChannelType.SlackUser, status: "pending"),
            NewItem(NotificationChannelType.SlackUser, status: "pending"),
            NewItem(NotificationChannelType.SlackUser, status: "failed", nextRetryAt: pastRetry),
            NewItem(NotificationChannelType.SlackUser, status: "failed", nextRetryAt: futureRetry), // excluded: not due
            // slack_team — included
            NewItem(NotificationChannelType.SlackTeam, status: "pending"),
            NewItem(NotificationChannelType.SlackTeam, status: "sent")); // excluded: not pending

        // Act
        var slackPending = await _repository!.GetPendingCountByChannelsAsync(
            new[] { NotificationChannelType.SlackUser, NotificationChannelType.SlackTeam },
            TestCancellationToken);
        var allChannelsPending = await _repository!.GetPendingCountAsync(TestCancellationToken);

        // Assert — Slack-scoped count = 2 su pending + 1 su failed-due + 1 st pending = 4,
        // and it excludes the email backlog that the unfiltered count still sees (4 email eligible).
        slackPending.Should().Be(4, "only slack_user + slack_team pending/retry-due rows count");
        allChannelsPending.Should().Be(8, "unfiltered count also includes the 4 eligible email rows");
        slackPending.Should().BeLessThan(allChannelsPending,
            "the email backlog must not be attributed to the Slack channels");
    }

    [Fact]
    public async Task GetPendingCountByChannelsAsync_SingleChannel_ScopesToThatChannelOnly()
    {
        // Arrange
        await SeedAsync(
            NewItem(NotificationChannelType.Email, status: "pending"),
            NewItem(NotificationChannelType.SlackUser, status: "pending"),
            NewItem(NotificationChannelType.SlackUser, status: "pending"),
            NewItem(NotificationChannelType.SlackTeam, status: "pending"));

        // Act
        var slackTeamOnly = await _repository!.GetPendingCountByChannelsAsync(
            new[] { NotificationChannelType.SlackTeam }, TestCancellationToken);

        // Assert
        slackTeamOnly.Should().Be(1, "only the single slack_team pending row matches");
    }

    [Fact]
    public async Task GetPendingCountByChannelsAsync_NoSlackBacklog_ReturnsZeroDespiteEmailBacklog()
    {
        // Arrange — the exact staging shape: backlog exists, but none of it is Slack.
        await SeedAsync(
            NewItem(NotificationChannelType.Email, status: "pending"),
            NewItem(NotificationChannelType.Email, status: "pending"),
            NewItem(NotificationChannelType.Email, status: "pending"));

        // Act
        var slackPending = await _repository!.GetPendingCountByChannelsAsync(
            new[] { NotificationChannelType.SlackUser, NotificationChannelType.SlackTeam },
            TestCancellationToken);

        // Assert
        slackPending.Should().Be(0, "an email-only backlog leaves the Slack-scoped count at zero");
    }

    private async Task SeedAsync(params NotificationQueueEntity[] items)
    {
        await _dbContext!.Set<NotificationQueueEntity>().AddRangeAsync(items, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static NotificationQueueEntity NewItem(
        NotificationChannelType channel, string status, DateTime? nextRetryAt = null)
    {
        return new NotificationQueueEntity
        {
            Id = Guid.NewGuid(),
            ChannelType = channel.Value,
            RecipientUserId = null,
            NotificationType = "share_request_created",
            Payload = "{}",
            Status = status,
            RetryCount = 0,
            MaxRetries = 3,
            NextRetryAt = nextRetryAt,
            CreatedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            SourceEventId = null,
        };
    }
}
