using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for OpenRouterRpmAlertBackgroundService.
/// Issue #5084: Verifies RPM alert notifications are sent to admins at the right threshold.
///
/// Tests:
/// - No limit configured (LimitRpm == 0) → early return, no notifications
/// - Below threshold → no notifications
/// - At threshold, first call (no cooldown) → notifications sent to admins
/// - At threshold, within cooldown window → no notifications (deduplicated)
/// - No admin users in DB → notifications not created
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OpenRouterRpmAlertBackgroundServiceTests : IDisposable
{
    private readonly Mock<IOpenRouterRateLimitTracker> _rateLimitTrackerMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IConfiguration _config;

    private static readonly Guid AdminId = Guid.NewGuid();

    public OpenRouterRpmAlertBackgroundServiceTests()
    {
        _rateLimitTrackerMock = new Mock<IOpenRouterRateLimitTracker>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Wire up scope factory chain
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
            .Returns(_dbContext);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(INotificationRepository)))
            .Returns(_notificationRepoMock.Object);

        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "OpenRouterAlerts:RpmThresholdPercent", "80" },
                { "OpenRouterAlerts:CooldownMinutes", "5" },
                { "OpenRouterAlerts:CheckIntervalSeconds", "30" }
            })
            .Build();
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private OpenRouterRpmAlertBackgroundService CreateSut() =>
        new(_scopeFactoryMock.Object, _rateLimitTrackerMock.Object, _config,
            NullLogger<OpenRouterRpmAlertBackgroundService>.Instance);

    private static RateLimitStatus MakeStatus(int current, int limit) => new()
    {
        CurrentRpm = current,
        LimitRpm = limit,
        UtilizationPercent = limit > 0 ? (double)current / limit : 0,
        IsThrottled = limit > 0 && current >= limit
    };

    private async Task InvokeCheckRpmAsync(OpenRouterRpmAlertBackgroundService sut)
    {
        var method = typeof(OpenRouterRpmAlertBackgroundService).GetMethod(
            "CheckRpmAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;
    }

    private async Task SeedAdminAsync()
    {
        _dbContext.Users.Add(new UserEntity
        {
            Id = AdminId,
            Email = "admin@test.com",
            Tier = "premium",
            Role = "admin"
        });
        await _dbContext.SaveChangesAsync();
    }

    // ── Guard: no limit configured ────────────────────────────────────────────

    [Fact]
    public async Task CheckRpm_WhenLimitIsZero_DoesNotCreateAnyScope()
    {
        // Arrange
        _rateLimitTrackerMock
            .Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatus(current: 0, limit: 0));

        var sut = CreateSut();

        // Act
        await InvokeCheckRpmAsync(sut);

        // Assert — no scope created, no notification
        _scopeFactoryMock.Verify(f => f.CreateScope(), Times.Never);
    }

    // ── Guard: below threshold ─────────────────────────────────────────────────

    [Fact]
    public async Task CheckRpm_WhenBelowThreshold_DoesNotCreateAnyScope()
    {
        // Arrange — 70% < 80% threshold
        _rateLimitTrackerMock
            .Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatus(current: 70, limit: 100));

        var sut = CreateSut();

        // Act
        await InvokeCheckRpmAsync(sut);

        // Assert
        _scopeFactoryMock.Verify(f => f.CreateScope(), Times.Never);
    }

    // ── Alert: at threshold, no prior alert ────────────────────────────────────

    [Fact]
    public async Task CheckRpm_AtThresholdWithAdmin_SendsNotification()
    {
        // Arrange — 85% > 80% threshold
        await SeedAdminAsync();
        _rateLimitTrackerMock
            .Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatus(current: 85, limit: 100));

        var sut = CreateSut();

        // Act
        await InvokeCheckRpmAsync(sut);

        // Assert — notification created for admin
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckRpm_AtThresholdWithNoAdmins_CreatesNoNotifications()
    {
        // Arrange — 90% > 80% threshold, but no admins in DB
        _rateLimitTrackerMock
            .Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatus(current: 90, limit: 100));

        var sut = CreateSut();

        // Act
        await InvokeCheckRpmAsync(sut);

        // Assert — scope was created (alert attempted) but no notification added
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Cooldown deduplication ────────────────────────────────────────────────

    [Fact]
    public async Task CheckRpm_SecondCallWithinCooldown_DoesNotSendSecondAlert()
    {
        // Arrange — 90% > 80% threshold; two consecutive calls
        await SeedAdminAsync();
        _rateLimitTrackerMock
            .Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatus(current: 90, limit: 100));

        var sut = CreateSut();

        // Act — first call sends alert and updates _lastAlertSentAt to UtcNow
        await InvokeCheckRpmAsync(sut);

        // Second call immediately (well within 5-minute cooldown)
        await InvokeCheckRpmAsync(sut);

        // Assert — only one notification total despite two calls
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Severity at 95% ───────────────────────────────────────────────────────

    [Fact]
    public async Task CheckRpm_At95PercentOrAbove_CreatesErrorSeverityNotification()
    {
        // Arrange — 96% ≥ 95% → Error severity
        await SeedAdminAsync();
        _rateLimitTrackerMock
            .Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatus(current: 96, limit: 100));

        var sut = CreateSut();

        Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification, CancellationToken>(
                (n, _) => captured = n)
            .Returns(Task.CompletedTask);

        // Act
        await InvokeCheckRpmAsync(sut);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured!.Severity.IsError);
    }

    [Fact]
    public async Task CheckRpm_Below95Percent_CreatesWarningSeverityNotification()
    {
        // Arrange — 85% < 95% → Warning severity
        await SeedAdminAsync();
        _rateLimitTrackerMock
            .Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatus(current: 85, limit: 100));

        var sut = CreateSut();

        Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification, CancellationToken>(
                (n, _) => captured = n)
            .Returns(Task.CompletedTask);

        // Act
        await InvokeCheckRpmAsync(sut);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured!.Severity.IsWarning);
    }
}
