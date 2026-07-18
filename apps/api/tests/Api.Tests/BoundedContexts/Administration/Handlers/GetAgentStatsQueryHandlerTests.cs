using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Services;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Handlers;

/// <summary>
/// Unit tests for GetAgentStatsQueryHandler.
/// #3122: the aggregate AverageLatency must be sample-weighted, not a mean-of-means
/// (which over-weights low-volume agents).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAgentStatsQueryHandlerTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly MeepleAiDbContext _dbContext;
    private readonly FakeHybridCache _cache;
    private readonly FakeTimeProvider _timeProvider;
    private readonly GetAgentStatsQueryHandler _handler;

    public GetAgentStatsQueryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _cache = new FakeHybridCache();
        _timeProvider = new FakeTimeProvider(FixedNow);
        _handler = new GetAgentStatsQueryHandler(
            _dbContext,
            _cache,
            Mock.Of<ILogger<GetAgentStatsQueryHandler>>(),
            _timeProvider);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_AggregatesLatency_SampleWeighted_NotMeanOfMeans()
    {
        // qa-agent: 10 executions @ 100ms; rules-agent: 1 execution @ 1000ms.
        // Per-agent means are 100 and 1000 → mean-of-means = 550.
        // Sample-weighted mean: (100*10 + 1000*1) / 11 = 2000/11 ≈ 181.8.
        SeedAgentLogs("qa-agent", count: 10, latencyMs: 100);
        SeedAgentLogs("rules-agent", count: 1, latencyMs: 1000);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAgentStatsQuery(), CancellationToken.None);

        result.Totals.TotalExecutions.Should().Be(11);
        result.Totals.AverageLatency.Should().BeApproximately(2000.0 / 11.0, 0.01);
        result.Totals.AverageLatency.Should().BeLessThan(300); // must NOT be the 550 mean-of-means
    }

    [Fact]
    public async Task Handle_NoLogs_ReturnsZeroAverageLatency()
    {
        var result = await _handler.Handle(new GetAgentStatsQuery(), CancellationToken.None);

        result.Totals.TotalExecutions.Should().Be(0);
        result.Totals.AverageLatency.Should().Be(0);
    }

    private void SeedAgentLogs(string agent, int count, int latencyMs)
    {
        // Within the default 30-day window and the 7-day "active" window.
        var createdAt = FixedNow.UtcDateTime.AddDays(-1);
        for (var i = 0; i < count; i++)
        {
            _dbContext.AiRequestLogs.Add(new AiRequestLogEntity
            {
                Id = Guid.NewGuid(),
                Endpoint = $"/agents/{agent}",
                LatencyMs = latencyMs,
                Status = "Success",
                CreatedAt = createdAt,
                TokenCount = 100,
                PromptTokens = 80,
                CompletionTokens = 20,
            });
        }
    }
}
