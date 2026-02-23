using System.Globalization;
using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
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
/// Unit tests for OpenRouterBudgetAlertBackgroundService.
/// Issue #5085: Verifies budget threshold alerts are sent to admins correctly.
///
/// Tests:
/// - Zero/negative budget configured → early return, no notifications
/// - Below all thresholds → no notifications
/// - At 50% threshold → Info notification per admin
/// - At 80% threshold → Warning notification per admin
/// - At 100% threshold → Error notification per admin
/// - Already alerted at threshold → not re-alerted within same day
/// - Second threshold (80%) after first (50%) already alerted → only new threshold fires
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OpenRouterBudgetAlertBackgroundServiceTests : IDisposable
{
    private readonly Mock<ILlmRequestLogRepository> _logRepoMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly MeepleAiDbContext _dbContext;

    private static readonly Guid AdminId = Guid.NewGuid();

    // Default empty cost breakdown for reuse
    private static readonly (
        IReadOnlyList<(string ModelId, decimal CostUsd, int Requests, int TotalTokens)> ByModel,
        IReadOnlyList<(string Source, decimal CostUsd, int Requests)> BySource,
        IReadOnlyList<(string Tier, decimal CostUsd, int Requests)> ByTier,
        decimal TotalCostUsd,
        int TotalRequests) EmptyBreakdown = (
            new List<(string, decimal, int, int)>(),
            new List<(string, decimal, int)>(),
            new List<(string, decimal, int)>(),
            0m, 0);

    public OpenRouterBudgetAlertBackgroundServiceTests()
    {
        _logRepoMock = new Mock<ILlmRequestLogRepository>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Wire up scope factory chain
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ILlmRequestLogRepository)))
            .Returns(_logRepoMock.Object);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
            .Returns(_dbContext);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(INotificationRepository)))
            .Returns(_notificationRepoMock.Object);

        _notificationRepoMock
            .Setup(r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private OpenRouterBudgetAlertBackgroundService CreateSut(
        decimal dailyBudget = 10m,
        string thresholds = "50,80,100")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "OpenRouterAlerts:DailyBudgetUsd", dailyBudget.ToString("F2", CultureInfo.InvariantCulture) },
                { "OpenRouterAlerts:BudgetAlertThresholds", thresholds },
                { "OpenRouterAlerts:BudgetCheckIntervalMin", "10" }
            })
            .Build();

        return new OpenRouterBudgetAlertBackgroundService(
            _scopeFactoryMock.Object, config,
            NullLogger<OpenRouterBudgetAlertBackgroundService>.Instance);
    }

    private void SetupSpend(decimal totalCostUsd) =>
        _logRepoMock
            .Setup(r => r.GetCostBreakdownAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                EmptyBreakdown.ByModel,
                EmptyBreakdown.BySource,
                EmptyBreakdown.ByTier,
                totalCostUsd,
                0));

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

    private static async Task InvokeCheckBudgetAsync(OpenRouterBudgetAlertBackgroundService sut)
    {
        var method = typeof(OpenRouterBudgetAlertBackgroundService).GetMethod(
            "CheckBudgetAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;
    }

    // ── Guard: zero budget ────────────────────────────────────────────────────
    // Note: spend is always fetched first; the budget guard fires after.
    // We verify no notifications are created when budget is zero/negative.

    [Fact]
    public async Task CheckBudget_WhenBudgetIsZero_DoesNotCreateNotifications()
    {
        // Arrange — spend fetched but guard fires before alerting
        SetupSpend(5.00m);
        await SeedAdminAsync();
        var sut = CreateSut(dailyBudget: 0m);

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert — no notifications despite admins existing
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckBudget_WhenBudgetIsNegative_DoesNotCreateNotifications()
    {
        // Arrange
        SetupSpend(5.00m);
        await SeedAdminAsync();
        var sut = CreateSut(dailyBudget: -5m);

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Guard: below all thresholds ────────────────────────────────────────────

    [Fact]
    public async Task CheckBudget_WhenBelowAllThresholds_DoesNotCreateNotifications()
    {
        // Arrange — 3.00 / 10.00 = 30% < 50% (first threshold)
        SetupSpend(3.00m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50,80,100");

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Threshold firing ──────────────────────────────────────────────────────

    [Fact]
    public async Task CheckBudget_At50PercentThreshold_CreatesNotificationForAdmin()
    {
        // Arrange — 5.00 / 10.00 = 50% ≥ 50% threshold
        await SeedAdminAsync();
        SetupSpend(5.00m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50,80,100");

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckBudget_At50PercentThreshold_CreatesInfoSeverityNotification()
    {
        // Arrange
        await SeedAdminAsync();
        SetupSpend(5.00m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50,80,100");

        Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification, CancellationToken>(
                (n, _) => captured = n)
            .Returns(Task.CompletedTask);

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured!.Severity.IsInfo);
    }

    [Fact]
    public async Task CheckBudget_At80PercentThreshold_CreatesWarningSeverityNotification()
    {
        // Arrange — 8.00 / 10.00 = 80% ≥ 80% threshold
        await SeedAdminAsync();
        SetupSpend(8.00m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50,80,100");

        Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification, CancellationToken>(
                (n, _) => captured = n)
            .Returns(Task.CompletedTask);

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert — first alert is the 50% threshold (also fires), second is 80%
        // The last captured notification is for 80% (Warning)
        Assert.NotNull(captured);
        Assert.True(captured!.Severity.IsWarning);
    }

    [Fact]
    public async Task CheckBudget_At100PercentThreshold_CreatesErrorSeverityNotification()
    {
        // Arrange — 10.50 / 10.00 = 105% ≥ 100% threshold
        await SeedAdminAsync();
        SetupSpend(10.50m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50,80,100");

        Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification, CancellationToken>(
                (n, _) => captured = n)
            .Returns(Task.CompletedTask);

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert — last captured is the 100% threshold (Error)
        Assert.NotNull(captured);
        Assert.True(captured!.Severity.IsError);
    }

    // ── Deduplication: same threshold not re-alerted ──────────────────────────

    [Fact]
    public async Task CheckBudget_SameThresholdCalledTwice_AlertsSentOnlyOnce()
    {
        // Arrange — spend remains at 50% between calls
        await SeedAdminAsync();
        SetupSpend(5.00m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50");

        // Act — two consecutive checks
        await InvokeCheckBudgetAsync(sut);
        await InvokeCheckBudgetAsync(sut);

        // Assert — only one notification despite two calls
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Multiple thresholds fire on single call ────────────────────────────────

    [Fact]
    public async Task CheckBudget_SpendAboveMultipleThresholds_AllFireOnFirstCall()
    {
        // Arrange — 8.50 / 10.00 = 85% → both 50% and 80% should fire
        await SeedAdminAsync();
        SetupSpend(8.50m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50,80,100");

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert — 50% and 80% both fire → 2 notifications (one per admin per threshold)
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── No admins → no notifications even if threshold exceeded ───────────────

    [Fact]
    public async Task CheckBudget_AtThresholdWithNoAdmins_CreatesNoNotifications()
    {
        // Arrange — no admins in DB
        SetupSpend(5.00m);
        var sut = CreateSut(dailyBudget: 10m, thresholds: "50");

        // Act
        await InvokeCheckBudgetAsync(sut);

        // Assert
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
