using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Integration;

/// <summary>
/// Regression tests for issue #2804 — DbContext concurrency + approve-status-not-committed.
///
/// The pre-existing <see cref="AccessRequestIntegrationTests"/> never caught these bugs because it
/// builds the DbContext with EF's DEFAULT tracking behaviour (TrackAll). The running app configures
/// <c>UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)</c> (PERF-06,
/// InfrastructureServiceExtensions.cs:178), so these tests reproduce the production config where the
/// bugs actually manifest.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class AccessRequest2804RegressionTests
{
    /// <summary>
    /// Builds a MeepleAiDbContext that mirrors the production tracking config (PERF-06 NoTracking).
    /// InMemory provider + shared database name so a fresh verification context reads the same store.
    /// </summary>
    private static MeepleAiDbContext CreateNoTrackingContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking) // PERF-06: prod default
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();
        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Symptom 2: approve does not persist Approved status under NoTracking config.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAccessRequest_UnderNoTrackingContext_PersistsApprovedStatus()
    {
        // Arrange — seed a pending request in a NoTracking (prod-like) context.
        var dbName = Guid.NewGuid().ToString();
        using (var seedContext = CreateNoTrackingContext(dbName))
        {
            var seedRepo = new AccessRequestRepository(seedContext, TestDbContextFactory.CreateMockEventCollector().Object);
            var request = AccessRequest.Create("approve-notracking@example.com");
            await seedRepo.AddAsync(request);
            await seedContext.SaveChangesAsync();
        }

        var adminId = Guid.NewGuid();
        Guid requestId;

        // Act — approve through the command handler using a NoTracking context (as prod does).
        using (var actContext = CreateNoTrackingContext(dbName))
        {
            var repo = new AccessRequestRepository(actContext, TestDbContextFactory.CreateMockEventCollector().Object);
            var pending = (await repo.GetByStatusAsync(AccessRequestStatus.Pending, page: 1, pageSize: 1)).Single();
            requestId = pending.Id;

            var handler = new ApproveAccessRequestCommandHandler(repo, new EfCoreUnitOfWork(actContext));
            await handler.Handle(new ApproveAccessRequestCommand(requestId, adminId), CancellationToken.None);
        }

        // Assert — read back from a FRESH context: the persisted store must reflect Approved.
        using var verifyContext = CreateNoTrackingContext(dbName);
        var verifyRepo = new AccessRequestRepository(verifyContext, TestDbContextFactory.CreateMockEventCollector().Object);
        var updated = await verifyRepo.GetByIdAsync(requestId);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(AccessRequestStatus.Approved, "the approve command must persist the status change even under the PERF-06 NoTracking default (#2804 symptom 2)");
        updated.ReviewedBy.Should().Be(adminId);
        updated.ReviewedAt.Should().NotBeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Symptom 1: request-access starts two concurrent lookups on the shared context.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestAccess_DoesNotStartConcurrentOperationsOnSharedContext()
    {
        // UserRepository and AccessRequestRepository share the SAME request-scoped MeepleAiDbContext.
        // A DbContext permits at most ONE in-flight async operation; EF's ConcurrencyDetector throws
        // "A second operation was started on this context instance..." otherwise. The handler must run
        // the two "timing equalization" lookups sequentially, not concurrently.
        //
        // The real detector needs a provider with genuine async I/O (Npgsql) to surface — InMemory does
        // not enforce it and Testcontainers Postgres is too timing-sensitive for a unit test. This guard
        // measures peak concurrency deterministically: each lookup blocks on a gate until Release() is
        // called, so when Handle() returns (having synchronously run up to its first blocking await) the
        // guard has already recorded how many lookups were started concurrently.
        var guard = new SharedContextConcurrencyGuard();

        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .Returns(() => guard.RunAsync<User?>(() => null));

        var accessRepo = new Mock<IAccessRequestRepository>();
        accessRepo
            .Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => guard.RunAsync<AccessRequest?>(() => null));
        accessRepo
            .Setup(r => r.AddAsync(It.IsAny<AccessRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new RequestAccessCommandHandler(accessRepo.Object, userRepo.Object, unitOfWork.Object);

        // Act — Handle() synchronously kicks off its lookups up to the first blocking await; by the time it
        // returns, the guard has recorded the peak number of simultaneously in-flight lookups.
        var handleTask = handler.Handle(new RequestAccessCommand("guest@example.com"), CancellationToken.None);
        guard.Release();       // let the in-flight lookups complete
        await handleTask;

        // Assert — the two lookups share one scoped DbContext, so at most ONE may be in flight at a time.
        guard.MaxConcurrent.Should().Be(1,
            "the request-access lookups share one scoped DbContext and must run sequentially, not concurrently (#2804 symptom 1)");
    }

    /// <summary>
    /// Deterministic probe for EF Core's per-DbContext single-operation rule. Every guarded operation
    /// blocks on a shared gate until <see cref="Release"/> is called, so <see cref="MaxConcurrent"/>
    /// records how many operations were started before any completed. Two operations started on the same
    /// (shared) context before the first is awaited ⇒ MaxConcurrent == 2, which the real ConcurrencyDetector
    /// would surface as "A second operation was started on this context instance...".
    /// </summary>
    private sealed class SharedContextConcurrencyGuard
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _inFlight;

        public int MaxConcurrent { get; private set; }

        public async Task<T> RunAsync<T>(Func<T> resultFactory)
        {
            var current = Interlocked.Increment(ref _inFlight);
            // MaxConcurrent is written only here — during the synchronous entry that runs on the caller's
            // thread before any suspension — so it needs no atomic even though _inFlight is decremented
            // later (post-Release) on a thread-pool thread.
            MaxConcurrent = Math.Max(MaxConcurrent, current);
            try
            {
                await _gate.Task.ConfigureAwait(false); // stay in-flight until the test releases the gate
                return resultFactory();
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public void Release() => _gate.TrySetResult();
    }
}
