using Api.BoundedContexts.Administration.Application.EventHandlers;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Health.Models;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.EventHandlers;

/// <summary>
/// Wiring tests for <see cref="HealthStatusChangedEventHandler.Handle"/>: verify the
/// handler actually populates the alert metadata with the <c>_suppress_email</c> flag for
/// non-critical (Degraded/recovery) transitions and omits it for Unhealthy (critical) ones.
/// This guards the exact code path that stops the flapping warning-mail spam — the static
/// helper <c>ShouldSuppressEmail</c> is covered separately, but the Handle→metadata wiring
/// (and its flow into AlertingService→EmailAlertChannel) needs its own coverage.
///
/// A relational DB provider is required for the idempotency guard; the InMemory provider
/// is non-relational (Database.IsRelational() == false), so the handler skips the DB reads
/// and writes and we can exercise the alerting path in isolation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class HealthStatusChangedEventHandlerWiringTests
{
    private static MeepleAiDbContext CreateInMemoryDb() =>
        new(
            new DbContextOptionsBuilder<MeepleAiDbContext>()
                .UseInMemoryDatabase($"health_handler_wiring_{Guid.NewGuid():N}")
                .Options,
            TestDbContextFactory.CreateMockMediator().Object,
            TestDbContextFactory.CreateMockEventCollector().Object);

    private static HealthStatusChangedEventHandler CreateHandler(
        MeepleAiDbContext db, Mock<IAlertingService> alerting)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IAlertingService))).Returns(alerting.Object);
        serviceProvider.Setup(p => p.GetService(typeof(MeepleAiDbContext))).Returns(db);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new HealthStatusChangedEventHandler(
            scopeFactory.Object,
            NullLogger<HealthStatusChangedEventHandler>.Instance);
    }

    [Fact]
    public async Task Handle_DegradedTransition_TagsMetadataForEmailSuppression()
    {
        // Arrange — a non-critical service flaps to Degraded (warning)
        var alerting = new Mock<IAlertingService>();
        using var db = CreateInMemoryDb();
        var handler = CreateHandler(db, alerting);

        var evt = new HealthStatusChangedEvent(
            ServiceName: "bggapi",
            PreviousStatus: "Healthy",
            CurrentStatus: "Degraded",
            Description: "BGG degraded",
            Tags: new[] { HealthCheckTags.External, HealthCheckTags.NonCritical },
            TransitionAt: DateTime.UtcNow,
            IsReminder: false);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — dispatched as a warning WITH the email-suppression flag set
        alerting.Verify(a => a.SendAlertAsync(
            "health.bggapi",
            "warning",
            It.IsAny<string>(),
            It.Is<IDictionary<string, object>>(m =>
                m.ContainsKey(EmailAlertChannel.SuppressEmailMetadataKey)
                && Equals(m[EmailAlertChannel.SuppressEmailMetadataKey], true)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnhealthyTransition_DoesNotSuppressEmail()
    {
        // Arrange — a service transitions to Unhealthy (critical); email must still go out
        var alerting = new Mock<IAlertingService>();
        using var db = CreateInMemoryDb();
        var handler = CreateHandler(db, alerting);

        var evt = new HealthStatusChangedEvent(
            ServiceName: "postgres",
            PreviousStatus: "Degraded",
            CurrentStatus: "Unhealthy",
            Description: "DB unreachable",
            Tags: new[] { HealthCheckTags.Core, HealthCheckTags.Critical },
            TransitionAt: DateTime.UtcNow,
            IsReminder: false);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — dispatched as critical WITHOUT the suppression flag
        alerting.Verify(a => a.SendAlertAsync(
            "health.postgres",
            "critical",
            It.IsAny<string>(),
            It.Is<IDictionary<string, object>>(m =>
                !m.ContainsKey(EmailAlertChannel.SuppressEmailMetadataKey)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
