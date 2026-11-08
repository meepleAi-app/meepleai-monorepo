using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// #4623: <c>GetAggregatedMetricsAsync</c> and <c>GetTimeSeriesMetricsAsync</c> built the bounds of the window with
/// <c>DateOnly.ToDateTime(TimeOnly.MinValue)</c>, which has <c>Kind=Unspecified</c>; Npgsql refuses to write that to a
/// <c>timestamp with time zone</c> column (<c>ArgumentException</c>, shown by the middleware as a 400). The four
/// <c>/rag-dashboard</c> metrics routes answered 400 to everybody, admins included.
/// </summary>
/// <remarks>
/// On Postgres, because the failure is in the parameter the provider writes: an in-memory provider never sees it.
/// The dates are relative to today, never fixed, because a fixed date ages. The rows sit on the edges of the window
/// (first and last instant of the range, one microsecond outside it), since fixing the <c>Kind</c> alone is not enough:
/// the upper bound has to keep including the whole last day.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "4623")]
public sealed class RagExecutionRepositoryDateRangeIntegrationTests : IAsyncLifetime
{
    private const string Strategy = "Hybrid";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private IRagExecutionRepository? _repository;

    private readonly DateOnly _firstDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
    private readonly DateOnly _lastDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    public RagExecutionRepositoryDateRangeIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rag_exec_range_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync(Ct);

        _repository = new RagExecutionRepository(
            _dbContext, _serviceProvider.GetRequiredService<IDomainEventCollector>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup: isolation is already guaranteed by the dedicated database.
            }
        }
    }

    /// <summary>
    /// The case of the issue: a window with dates is answered, and it counts the runs of its first and last day (to
    /// the last microsecond) and none of the instants just outside it.
    /// </summary>
    [Fact]
    public async Task TheAggregatedMetrics_ForADateRange_CountTheWholeRange_AndNothingOutsideIt()
    {
        await SeedEdgesAsync();

        var metrics = await _repository!.GetAggregatedMetricsAsync(_firstDay, _lastDay, strategy: null, Ct);

        metrics.Should().ContainSingle().Which.TotalQueries.Should().Be(2,
            "the run at 00:00:00 of the first day and the one at 23:59:59.999999 of the last day are inside; the one " +
            "a microsecond before the range and the one at midnight after it are not");
    }

    /// <summary>The same window with one bound only: each bound is built on its own.</summary>
    [Fact]
    public async Task TheAggregatedMetrics_WithOneBoundOnly_ApplyThatBound()
    {
        await SeedEdgesAsync();

        var fromFirstDay = await _repository!.GetAggregatedMetricsAsync(_firstDay, endDate: null, strategy: null, Ct);
        var untilLastDay = await _repository.GetAggregatedMetricsAsync(startDate: null, _lastDay, strategy: null, Ct);

        fromFirstDay.Should().ContainSingle().Which.TotalQueries.Should().Be(3, "everything from the first instant on");
        untilLastDay.Should().ContainSingle().Which.TotalQueries.Should().Be(3, "everything up to the last instant of the last day");
    }

    /// <summary>The strategy filter still composes with the window.</summary>
    [Fact]
    public async Task TheAggregatedMetrics_ForADateRange_StillFilterByStrategy()
    {
        await SeedEdgesAsync();
        await SeedAsync(AtStartOf(_firstDay).AddHours(12), strategy: "Semantic");

        var hybrid = await _repository!.GetAggregatedMetricsAsync(_firstDay, _lastDay, Strategy, Ct);

        hybrid.Should().ContainSingle().Which.TotalQueries.Should().Be(2);
        hybrid[0].Strategy.Should().Be(Strategy);
    }

    /// <summary>
    /// The time series of the issue: same window, daily buckets. The last day has its bucket, and the points outside the
    /// range are not there.
    /// </summary>
    [Fact]
    public async Task TheTimeSeries_ForADateRange_HasABucketForEachEdgeDay_AndNoneOutsideIt()
    {
        await SeedEdgesAsync();

        var series = await _repository!.GetTimeSeriesMetricsAsync(
            Strategy, _firstDay, _lastDay, TimeSeriesGranularity.Day, Ct);

        series.Select(p => DateOnly.FromDateTime(p.Bucket.UtcDateTime)).Should().Equal(
            [_firstDay, _lastDay],
            "one run on the first day, one on the last; the neighbours of the range are out");
        series.Should().OnlyContain(p => p.QueryCount == 1);
    }

    // ─── Support ────────────────────────────────────────────────

    private static DateTime AtStartOf(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>
    /// Four runs of the same strategy around the window: a microsecond before it, its first instant, its last
    /// instant (23:59:59.999999, the finest instant Postgres keeps), and midnight after it.
    /// </summary>
    private async Task SeedEdgesAsync()
    {
        await SeedAsync(AtStartOf(_firstDay).AddTicks(-10));
        await SeedAsync(AtStartOf(_firstDay));
        await SeedAsync(AtStartOf(_lastDay.AddDays(1)).AddTicks(-10));
        await SeedAsync(AtStartOf(_lastDay.AddDays(1)));
    }

    private async Task SeedAsync(DateTime createdAt, string strategy = Strategy)
    {
        var execution = RagExecution.Create(
            query: "How many cards does each player draw at setup?",
            agentDefinitionId: null,
            agentName: "Rules Agent",
            strategy: strategy,
            model: "test-model",
            provider: "test-provider",
            gameId: null,
            isPlayground: false,
            totalLatencyMs: 1200,
            promptTokens: 400,
            completionTokens: 112,
            totalTokens: 512,
            totalCost: 0.0123m,
            confidence: 0.42,
            cacheHit: false,
            status: "Success",
            errorMessage: null,
            executionTrace: null);
        _dbContext!.Set<RagExecution>().Add(execution);
        await _dbContext.SaveChangesAsync(Ct);

        // Create stamps "now"; the instant is set afterwards, the way the rows really age.
        await _dbContext.Set<RagExecution>()
            .Where(e => e.Id == execution.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, createdAt), Ct);
    }
}
