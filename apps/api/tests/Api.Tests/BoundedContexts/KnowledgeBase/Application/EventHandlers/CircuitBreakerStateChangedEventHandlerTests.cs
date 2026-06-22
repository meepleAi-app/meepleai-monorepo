using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for CircuitBreakerStateChangedEventHandler.
/// Issue #5086: Verifies admin notification creation on circuit breaker state transitions.
///
/// Tests:
/// - No admins in DB → no notifications created
/// - Open state → Error severity notification per admin
/// - HalfOpen state → Warning severity notification per admin
/// - Closed state → Success severity notification per admin
/// - Title contains state keyword and provider name
/// - Message equals event reason
/// - Multiple admins → one notification each
/// - Repository exception → swallowed, not propagated
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CircuitBreakerStateChangedEventHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly CircuitBreakerStateChangedEventHandler _handler;

    private static readonly Guid AdminId = Guid.NewGuid();

    public CircuitBreakerStateChangedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _handler = new CircuitBreakerStateChangedEventHandler(
            _notificationRepoMock.Object,
            _dbContext,
            NullLogger<CircuitBreakerStateChangedEventHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task SeedAdminAsync(Guid? id = null)
    {
        _dbContext.Users.Add(new UserEntity
        {
            Id = id ?? AdminId,
            Email = $"{(id ?? AdminId):N}@test.com",
            Tier = "premium",
            Role = "admin"
        });
        await _dbContext.SaveChangesAsync();
    }

    private static CircuitBreakerStateChangedEvent MakeEvent(
        CircuitState newState,
        string provider = "openrouter",
        string reason = "Test reason") =>
        new(provider, CircuitState.Closed, newState, reason, DateTime.UtcNow);

    // ── Guard: no admins ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoAdmins_DoesNotCreateAnyNotification()
    {
        // Arrange
        var evt = MakeEvent(CircuitState.Open);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepoMock.Verify(
            r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Severity mapping ─────────────────────────────────────────────────────

    // CircuitState is internal — cannot use [InlineData] with a public method parameter.
    // Use separate [Fact] methods backed by a private helper.
    private async Task VerifySeverityForState(CircuitState newState, NotificationSeverity expectedSeverity)
    {
        await SeedAdminAsync();
        var evt = MakeEvent(newState);

        List<Notification>? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((ns, _) => captured = ns.ToList())
            .ReturnsAsync(1);

        await _handler.Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Should().ContainSingle();
        captured[0].Severity.Should().Be(expectedSeverity);
        captured[0].Type.Should().Be(NotificationType.AdminSystemHealthAlert);
        captured[0].Link.Should().Be("/admin/agents/usage");
    }

    [Fact]
    public Task Handle_OpenState_CreatesErrorSeverityNotification() =>
        VerifySeverityForState(CircuitState.Open, NotificationSeverity.Error);

    [Fact]
    public Task Handle_HalfOpenState_CreatesWarningSeverityNotification() =>
        VerifySeverityForState(CircuitState.HalfOpen, NotificationSeverity.Warning);

    [Fact]
    public Task Handle_ClosedState_CreatesSuccessSeverityNotification() =>
        VerifySeverityForState(CircuitState.Closed, NotificationSeverity.Success);

    // ── Title content ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OpenState_TitleContainsOPENEDAndProvider()
    {
        // Arrange
        await SeedAdminAsync();
        var evt = MakeEvent(CircuitState.Open, provider: "openrouter");

        List<Notification>? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((ns, _) => captured = ns.ToList())
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        captured!.Should().ContainSingle();
        captured[0].Title.Should().Contain("OPENED");
        captured[0].Title.Should().Contain("openrouter");
    }

    [Fact]
    public async Task Handle_ClosedState_TitleContainsRecovered()
    {
        // Arrange
        await SeedAdminAsync();
        var evt = MakeEvent(CircuitState.Closed, provider: "ollama");

        List<Notification>? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((ns, _) => captured = ns.ToList())
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        captured!.Should().ContainSingle();
        captured[0].Title.Should().ContainEquivalentOf("recovered");
        captured[0].Title.Should().Contain("ollama");
    }

    // ── Message equals reason ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithAdmin_MessageEqualsEventReason()
    {
        // Arrange
        await SeedAdminAsync();
        const string reason = "Provider returned 503 three consecutive times";
        var evt = MakeEvent(CircuitState.Open, reason: reason);

        List<Notification>? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((ns, _) => captured = ns.ToList())
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        captured!.Should().ContainSingle();
        captured[0].Message.Should().Be(reason);
    }

    // ── Multiple admins ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MultipleAdmins_CreatesOneNotificationPerAdmin()
    {
        // Arrange — two admins
        var adminId2 = Guid.NewGuid();
        await SeedAdminAsync(AdminId);
        await SeedAdminAsync(adminId2);

        var evt = MakeEvent(CircuitState.Open);

        List<Notification>? captured = null;
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((ns, _) => captured = ns.ToList())
            .ReturnsAsync(2);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        captured!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_MultipleAdmins_EachNotificationHasCorrectUserId()
    {
        // Arrange — two admins
        var adminId2 = Guid.NewGuid();
        await SeedAdminAsync(AdminId);
        await SeedAdminAsync(adminId2);

        var evt = MakeEvent(CircuitState.Open);

        List<Guid>? capturedUserIds = null;
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((ns, _) => capturedUserIds = ns.Select(n => n.UserId).ToList())
            .ReturnsAsync(2);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        capturedUserIds.Should().Contain(AdminId);
        capturedUserIds.Should().Contain(adminId2);
    }

    // ── Exception swallowing ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRepositoryThrows_DoesNotPropagate()
    {
        // Arrange
        await SeedAdminAsync();
        _notificationRepoMock
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var evt = MakeEvent(CircuitState.Open);

        // Act + Assert — must not rethrow
        await _handler.Handle(evt, CancellationToken.None);
    }
}
