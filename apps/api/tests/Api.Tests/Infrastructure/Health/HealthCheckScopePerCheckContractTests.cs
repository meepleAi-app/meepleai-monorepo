using Api.Infrastructure;
using Api.Infrastructure.Health.Checks;
using Api.Infrastructure.HealthChecks;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Health;

/// <summary>
/// Issue #4059, correction of the premise. Three registered health checks inject the scoped
/// <see cref="MeepleAiDbContext"/> — <see cref="SeedStateHealthCheck"/>,
/// <see cref="LiveSessionPersistenceHealthCheck"/>, <see cref="SharedGameCatalogHealthCheck"/> — and
/// <c>DefaultHealthCheckService</c> runs every check <b>concurrently</b>. The obvious conclusion is
/// that they share one <c>DbContext</c> and trip EF Core's "A second operation was started on this
/// context instance"; that is what #4059 was filed as, and it is <b>not</b> what happens.
///
/// <para>
/// Measured here: .NET 9's <c>DefaultHealthCheckService</c> creates a DI scope <b>per check</b>
/// (<c>RunCheckAsync</c> opens its own scope), so each DB-bound check gets a context of its own and
/// the concurrency is harmless. The 500 on <c>/api/v1/admin/kb/pipeline/health</c> came from the
/// endpoint's own <c>Task.WhenAll</c> over the request-scoped context — see
/// <c>AdminPipelineHealthSendSerializationTests</c>. In the very request that answered 500, every
/// health check in the log completed normally.
/// </para>
///
/// <para>
/// 🔴 This is a framework-contract test, not a product test: it is expected to stay green, and its
/// job is to go red if a future runtime goes back to one shared scope. That regression would be
/// close to invisible otherwise — each DB-bound check catches its own exception and returns
/// Degraded/Unhealthy, so <c>/health</c> would start degrading with an EF threading message buried
/// in an entry's exception, and the three checks would fail by execution order (whichever two
/// overlapped), not reproducibly. If this test fails, the three checks above need a context each
/// (<c>IDbContextFactory</c>) before anything else is investigated.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Issue", "4059")]
public sealed class HealthCheckScopePerCheckContractTests
{
    private const int CheckCount = 3;

    /// <summary>
    /// A check that records the <c>MeepleAiDbContext</c> instance its scope handed it, then blocks
    /// until every sibling has arrived. Blocking is what makes "they run at the same time" an
    /// observation rather than an assumption: if the service ran them one after another, the first
    /// would wait out the timeout and report <c>alone</c>.
    /// </summary>
    private sealed class ContextRecordingCheck : IHealthCheck
    {
        private readonly MeepleAiDbContext _db;
        private readonly Rendezvous _rendezvous;

        public ContextRecordingCheck(MeepleAiDbContext db, Rendezvous rendezvous)
        {
            _db = db;
            _rendezvous = rendezvous;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            _rendezvous.Contexts.Add(_db);
            var concurrent = await _rendezvous.ArriveAndWaitForTheOthersAsync().ConfigureAwait(false);
            return concurrent
                ? HealthCheckResult.Healthy("ran alongside its siblings")
                : HealthCheckResult.Unhealthy("ran alone");
        }
    }

    private sealed class Rendezvous
    {
        private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public System.Collections.Concurrent.ConcurrentBag<MeepleAiDbContext> Contexts { get; } = new();

        public async Task<bool> ArriveAndWaitForTheOthersAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= CheckCount)
            {
                _all.TrySetResult();
            }

            // A sequential runner never completes the rendezvous; the timeout is the escape hatch
            // that turns a would-be hang into the readable "ran alone" verdict.
            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            return await Task.WhenAny(_all.Task, timeout).ConfigureAwait(false) == _all.Task;
        }
    }

    private static ServiceProvider BuildHost(Rendezvous rendezvous)
    {
        var databaseName = $"HealthCheckScopeContract_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddLogging();

        // Registered exactly as production registers it: scoped. The whole question is which scope
        // a health check resolves it from.
        services.AddScoped(_ => new MeepleAiDbContext(
            new DbContextOptionsBuilder<MeepleAiDbContext>().UseInMemoryDatabase(databaseName).Options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>()));

        var builder = services.AddHealthChecks();
        for (var i = 0; i < CheckCount; i++)
        {
            builder.Add(new HealthCheckRegistration(
                $"db-bound-{i}",
                sp => new ContextRecordingCheck(sp.GetRequiredService<MeepleAiDbContext>(), rendezvous),
                HealthStatus.Unhealthy,
                tags: null));
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task EveryCheckGetsItsOwnDbContext_SoConcurrentChecksCannotCollide()
    {
        var rendezvous = new Rendezvous();
        await using var provider = BuildHost(rendezvous);

        var report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        report.Entries.Should().HaveCount(CheckCount);
        report.Entries.Values.Should().AllSatisfy(entry => entry.Status.Should().Be(
            HealthStatus.Healthy,
            "the checks must overlap in time — the premise of #4059 rests on that, and so does the " +
            "assertion below being meaningful"));

        rendezvous.Contexts.Should().HaveCount(CheckCount);
        rendezvous.Contexts.Distinct().Should().HaveCount(
            CheckCount,
            "DefaultHealthCheckService opens a DI scope per check, so the DB-bound checks that " +
            "inject MeepleAiDbContext each get their own; if this is ever one instance, those " +
            "checks are racing on a shared context and need IDbContextFactory");
    }
}
