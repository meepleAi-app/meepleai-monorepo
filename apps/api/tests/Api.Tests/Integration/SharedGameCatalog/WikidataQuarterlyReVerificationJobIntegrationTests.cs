using System.Diagnostics.Metrics;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Observability;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration tests for <see cref="WikidataQuarterlyReVerificationJob"/>
/// against a real PostgreSQL via Testcontainers. Exercises the
/// <c>ExecuteUpdateAsync</c> LINQ → SQL translation that unit tests cannot
/// reproduce. Issue #1823 Wave 3 M15 (ADR DEC-3i).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
[Collection("Integration-GroupA")]
public class WikidataQuarterlyReVerificationJobIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_wqrv_job_{Guid.NewGuid():N}";

    private static readonly DateTime FixedNow = new(2026, 6, 11, 4, 0, 0, DateTimeKind.Utc);

    public WikidataQuarterlyReVerificationJobIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task RunOnceAsync_ResetsStaleVerifiedAt_PreservesFreshAndUnverifiedAndQidless()
    {
        // 4 categories:
        //  (1) stale verified (>90d ago) → MUST reset to NULL
        //  (2) fresh verified (<90d ago) → MUST stay
        //  (3) qid set, never verified (already NULL) → MUST stay NULL (no-op)
        //  (4) qid NULL → MUST stay untouched (filter excludes it)
        var staleGame = SeedGame(qid: "Q1",
            lastVerifiedAt: FixedNow.AddDays(-120));
        var freshGame = SeedGame(qid: "Q2",
            lastVerifiedAt: FixedNow.AddDays(-30));
        var unverifiedGame = SeedGame(qid: "Q3",
            lastVerifiedAt: null);
        var qidlessGame = SeedGame(qid: null,
            lastVerifiedAt: null);

        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var job = new WikidataQuarterlyReVerificationJob(
            serviceProvider: Mock.Of<IServiceProvider>(),
            timeProvider: new FakeTimeProvider(new DateTimeOffset(FixedNow, TimeSpan.Zero)),
            logger: NullLogger<WikidataQuarterlyReVerificationJob>.Instance);

        var reset = await job.RunOnceAsync(_dbContext, TestContext.Current.CancellationToken);

        reset.Should().Be(1, "only the stale-verified game has WikidataQidLastVerifiedAt > 90d old");

        // ExecuteUpdateAsync bypasses the change tracker → reload via fresh read.
        _dbContext.ChangeTracker.Clear();
        var games = await _dbContext.SharedGames.AsNoTracking()
            .ToDictionaryAsync(g => g.Id, TestContext.Current.CancellationToken);

        games[staleGame.Id].WikidataQidLastVerifiedAt
            .Should().BeNull("stale verification MUST be reset to NULL so M9 picks it up");
        games[freshGame.Id].WikidataQidLastVerifiedAt
            .Should().NotBeNull("fresh verification is inside the 90d window");
        games[unverifiedGame.Id].WikidataQidLastVerifiedAt
            .Should().BeNull("already-null verification is untouched");
        games[qidlessGame.Id].WikidataQidLastVerifiedAt
            .Should().BeNull("QID-less game is ignored by the WHERE clause");
    }

    [Fact]
    public async Task RunOnceAsync_NoStaleRows_ReturnsZero()
    {
        SeedGame(qid: "Q1", lastVerifiedAt: FixedNow.AddDays(-10));
        SeedGame(qid: "Q2", lastVerifiedAt: FixedNow.AddDays(-89));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var job = new WikidataQuarterlyReVerificationJob(
            Mock.Of<IServiceProvider>(),
            new FakeTimeProvider(new DateTimeOffset(FixedNow, TimeSpan.Zero)),
            NullLogger<WikidataQuarterlyReVerificationJob>.Instance);

        var reset = await job.RunOnceAsync(_dbContext, TestContext.Current.CancellationToken);

        reset.Should().Be(0);
    }

    [Fact]
    public async Task RunOnceAsync_Idempotent_SecondRunResetsNothing()
    {
        SeedGame(qid: "Q1", lastVerifiedAt: FixedNow.AddDays(-120));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var job = new WikidataQuarterlyReVerificationJob(
            Mock.Of<IServiceProvider>(),
            new FakeTimeProvider(new DateTimeOffset(FixedNow, TimeSpan.Zero)),
            NullLogger<WikidataQuarterlyReVerificationJob>.Instance);

        var first = await job.RunOnceAsync(_dbContext, TestContext.Current.CancellationToken);
        var second = await job.RunOnceAsync(_dbContext, TestContext.Current.CancellationToken);

        first.Should().Be(1);
        second.Should().Be(0, "the first pass already set WikidataQidLastVerifiedAt = NULL");
    }

    [Fact]
    public async Task RunOnceAsync_EmitsPrometheusCounter_OnNonZeroReset()
    {
        // Issue #2055 Phase G AC-G1 — metric must fire only when reset > 0.
        SeedGame(qid: "Q1", lastVerifiedAt: FixedNow.AddDays(-120));
        SeedGame(qid: "Q2", lastVerifiedAt: FixedNow.AddDays(-100));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        long observed = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "meepleai.wikidata.quarterly_reverification.reset.total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            observed += measurement;
        });
        listener.Start();

        var job = new WikidataQuarterlyReVerificationJob(
            Mock.Of<IServiceProvider>(),
            new FakeTimeProvider(new DateTimeOffset(FixedNow, TimeSpan.Zero)),
            NullLogger<WikidataQuarterlyReVerificationJob>.Instance);

        var reset = await job.RunOnceAsync(_dbContext, TestContext.Current.CancellationToken);

        reset.Should().Be(2);
        observed.Should().Be(2, "the counter MUST record the reset count exactly once");
    }

    [Fact]
    public async Task RunOnceAsync_DoesNotEmitCounter_OnZeroReset()
    {
        SeedGame(qid: "Q1", lastVerifiedAt: FixedNow.AddDays(-10));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        long observed = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "meepleai.wikidata.quarterly_reverification.reset.total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            observed += measurement;
        });
        listener.Start();

        var job = new WikidataQuarterlyReVerificationJob(
            Mock.Of<IServiceProvider>(),
            new FakeTimeProvider(new DateTimeOffset(FixedNow, TimeSpan.Zero)),
            NullLogger<WikidataQuarterlyReVerificationJob>.Instance);

        var reset = await job.RunOnceAsync(_dbContext, TestContext.Current.CancellationToken);

        reset.Should().Be(0);
        observed.Should().Be(0, "zero-reset ticks MUST NOT emit the counter (avoids noise)");
    }

    private SharedGameEntity SeedGame(string? qid, DateTime? lastVerifiedAt)
    {
        var entity = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Game {qid ?? Guid.NewGuid().ToString("N")}",
            YearPublished = 2020,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WikidataQid = qid,
            WikidataQidLastVerifiedAt = lastVerifiedAt,
        };
        _dbContext!.SharedGames.Add(entity);
        return entity;
    }
}
