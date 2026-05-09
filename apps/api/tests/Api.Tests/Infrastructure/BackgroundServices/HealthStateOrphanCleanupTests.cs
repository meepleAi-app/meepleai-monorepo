using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities.Administration;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="HealthStateOrphanCleanup"/> (Issue #885).
/// Verifies that rows for unregistered services are removed and rows for active
/// services are preserved.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "885")]
public sealed class HealthStateOrphanCleanupTests
{
    private static MeepleAiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"orphan-cleanup-{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    private static ServiceHealthStateEntity SeedRow(string serviceName) => new()
    {
        Id = Guid.NewGuid(),
        ServiceName = serviceName,
        CurrentStatus = "Healthy",
        PreviousStatus = "Healthy",
        ConsecutiveFailures = 0,
        ConsecutiveSuccesses = 1,
        LastTransitionAt = DateTime.UtcNow.AddDays(-30),
        Tags = "[]",
        CreatedAt = DateTime.UtcNow.AddDays(-30),
        UpdatedAt = DateTime.UtcNow.AddDays(-30),
    };

    [Fact]
    public async Task RemoveOrphansAsync_With_All_Registered_Removes_Nothing()
    {
        using var db = CreateContext();
        db.ServiceHealthStates.AddRange(
            SeedRow("postgres"),
            SeedRow("redis"),
            SeedRow("openrouter"));
        await db.SaveChangesAsync();

        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "postgres", "redis", "openrouter"
        };

        var removed = await HealthStateOrphanCleanup.RemoveOrphansAsync(db, registered, CancellationToken.None);

        removed.Should().BeEmpty();
        (await db.ServiceHealthStates.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task RemoveOrphansAsync_With_Some_Unregistered_Removes_Only_Orphans()
    {
        // Reproduces the staging scenario after PR #883 deploy: unstructured/smoldocling/
        // ollama were Degraded for a month, then de-registered. Their rows must be cleaned
        // up while postgres/redis/openrouter rows are preserved untouched.
        using var db = CreateContext();
        db.ServiceHealthStates.AddRange(
            SeedRow("postgres"),
            SeedRow("redis"),
            SeedRow("openrouter"),
            SeedRow("unstructured"),
            SeedRow("smoldocling"),
            SeedRow("ollama"));
        await db.SaveChangesAsync();

        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "postgres", "redis", "openrouter"
        };

        var removed = await HealthStateOrphanCleanup.RemoveOrphansAsync(db, registered, CancellationToken.None);

        removed.Should().BeEquivalentTo(new[] { "unstructured", "smoldocling", "ollama" });

        var remaining = await db.ServiceHealthStates.Select(r => r.ServiceName).ToListAsync();
        remaining.Should().BeEquivalentTo(new[] { "postgres", "redis", "openrouter" });
    }

    [Fact]
    public async Task RemoveOrphansAsync_Match_Is_Case_Insensitive()
    {
        // Defensive: HealthCheckServiceOptions.Registrations may differ in casing across
        // EF Core versions or registration paths. The cleanup must not delete a real
        // service just because casing drifted.
        using var db = CreateContext();
        db.ServiceHealthStates.Add(SeedRow("PostGres"));
        await db.SaveChangesAsync();

        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "postgres" };

        var removed = await HealthStateOrphanCleanup.RemoveOrphansAsync(db, registered, CancellationToken.None);

        removed.Should().BeEmpty();
        (await db.ServiceHealthStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RemoveOrphansAsync_With_Empty_DB_Returns_Empty()
    {
        using var db = CreateContext();
        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "postgres" };

        var removed = await HealthStateOrphanCleanup.RemoveOrphansAsync(db, registered, CancellationToken.None);

        removed.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveOrphansAsync_With_Empty_Registered_Set_Wipes_All_Rows_And_Caller_Must_Guard()
    {
        // CONTRACT: the helper does NOT special-case an empty registeredNames set —
        // it removes every row, by design. This is *correct* mathematically (no row
        // matches an empty whitelist) but DANGEROUS in production where an empty set
        // can result from a DI startup race or misconfigured host.
        //
        // The consumer (InfrastructureHealthMonitorService.HydrateStateFromDatabaseAsync)
        // guards against this case explicitly. This test pins the helper's contract so a
        // future refactor that "fixes" the helper to skip empty sets does not silently
        // change the contract assumed by the consumer.
        //
        // See the matching guard:
        //   InfrastructureHealthMonitorService.cs — "if (registeredNames.Count == 0)".
        using var db = CreateContext();
        db.ServiceHealthStates.AddRange(SeedRow("foo"), SeedRow("bar"));
        await db.SaveChangesAsync();

        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var removed = await HealthStateOrphanCleanup.RemoveOrphansAsync(db, registered, CancellationToken.None);

        removed.Should().BeEquivalentTo(new[] { "foo", "bar" });
        (await db.ServiceHealthStates.CountAsync()).Should().Be(0);
    }
}
