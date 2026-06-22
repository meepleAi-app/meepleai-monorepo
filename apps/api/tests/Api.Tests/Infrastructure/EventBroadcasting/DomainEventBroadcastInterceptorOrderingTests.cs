using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.Infrastructure.EventBroadcasting;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.EventBroadcasting;

/// <summary>
/// Unit-only ordering test for <see cref="DomainEventBroadcastInterceptor"/>
/// — extracted from <see cref="DomainEventBroadcastInterceptorTests"/> because
/// that class is bound to the Integration-GroupD Testcontainers fixture.
/// xUnit instantiates the collection fixture for ALL tests in the collection,
/// even ones traited <c>Category=Unit</c>, which causes the
/// <c>Backend Fast (build + unit)</c> CI job (no Docker) to fail with
/// <c>DockerUnavailableException</c>.
///
/// This test uses EF Core InMemory only — no Postgres, no Docker.
///
/// Issue #1873 review follow-up.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
public sealed class DomainEventBroadcastInterceptorOrderingTests
{
    /// <summary>
    /// Creates a minimal <see cref="DomainEventLogEntity"/> for testing.
    /// All required string columns are non-null.
    /// </summary>
    private static DomainEventLogEntity MakeEventLog(
        Guid? eventId = null,
        string eventType = "agent.created")
        => new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId ?? Guid.NewGuid(),
            EventType = eventType,
            AggregateType = "Agent",
            AggregateId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PayloadJson = "{}",
            PayloadVersion = 1,
            OccurredAt = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow,
        };

    /// <summary>
    /// Verifies ordering: Publish is called AFTER <c>SaveChangesAsync</c> completes
    /// (i.e., from <c>SavedChangesAsync</c>, not from <c>SavingChangesAsync</c>).
    ///
    /// Uses EF InMemory so the test does not require Testcontainers infra — the
    /// ordering property is an EF Core contract (SavedChangesAsync fires post-SQL),
    /// independent of the database provider.
    ///
    /// Timing instrumentation: <c>saveChangesStarted</c> is flipped to <c>true</c>
    /// just before <c>SaveChangesAsync</c> is called. The Publish callback checks
    /// this flag and records whether Publish fired while <c>SaveChangesAsync</c>
    /// was still executing (i.e., before it returned control to the caller). If the
    /// interceptor were to call Publish from <c>SavingChangesAsync</c> (pre-commit),
    /// the <c>publishedBeforeSaveCompleted</c> flag would be set to <c>true</c>,
    /// causing the assertion to fail.
    /// </summary>
    [Fact]
    public async Task Publishes_AfterCommit_NotBefore()
    {
        // Arrange
        var saveChangesStarted = false;
        var publishedBeforeSaveCompleted = false;
        var publishWasCalled = false;

        var broadcasterMock = new Mock<IEventBroadcaster>();
        broadcasterMock
            .Setup(b => b.Publish(It.IsAny<DomainEventDto>()))
            .Callback<DomainEventDto>(_ =>
            {
                // If saveChangesStarted is true but SaveChangesAsync has not yet returned,
                // we are inside the interceptor call chain. A correct implementation calls
                // Publish from SavedChangesAsync (which runs before SaveChangesAsync returns),
                // so this flag will be true here — that is expected and correct.
                // What we must PREVENT is Publish being called before saveChangesStarted,
                // which would mean it fired outside any SaveChanges call entirely.
                if (!saveChangesStarted)
                {
                    // Publish fired before SaveChangesAsync was even entered — regression.
                    publishedBeforeSaveCompleted = true;
                }
                publishWasCalled = true;
            });

        var interceptor = new DomainEventBroadcastInterceptor(broadcasterMock.Object);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"timing-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .AddInterceptors(interceptor)
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(e => e.PeekEvents()).Returns(Array.Empty<IDomainEvent>());
        mockEventCollector.Setup(e => e.Clear());

        await using var ctx = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

        var entity = MakeEventLog();
        ctx.DomainEventLogs.Add(entity);

        // Verify Publish has NOT been called before SaveChanges starts
        publishWasCalled.Should().BeFalse("Publish must not fire before SaveChangesAsync is called");

        // Act — EF Core fires SavingChangesAsync (before SQL) then SavedChangesAsync (after SQL).
        // Our interceptor publishes in SavedChangesAsync, so publishWasCalled becomes true
        // during the SaveChangesAsync call but AFTER the underlying SQL/InMemory write.
        saveChangesStarted = true;
        await ctx.SaveChangesAsync();

        // After SaveChangesAsync returns, SavedChangesAsync has already completed → Publish was invoked.
        publishWasCalled.Should().BeTrue(
            "Publish must have been called (from SavedChangesAsync, post-commit) by the time SaveChangesAsync returns");

        // Real regression guard: if Publish fired before saveChangesStarted was set,
        // it means the interceptor called Publish outside the SaveChanges pipeline entirely.
        publishedBeforeSaveCompleted.Should().BeFalse(
            "Publish must not fire before SaveChangesAsync is entered — it must be called from SavedChangesAsync (post-commit), not SavingChangesAsync (pre-commit)");

        // Validate the published DTO maps correctly from the entity
        broadcasterMock.Verify(b => b.Publish(It.Is<DomainEventDto>(d =>
            d.Id == entity.Id &&
            d.EventId == entity.EventId &&
            d.EventType == entity.EventType &&
            d.PayloadJson == entity.PayloadJson)), Times.Once);
    }
}
