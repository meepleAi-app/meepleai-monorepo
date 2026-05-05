// Issue #614 (Wave A.4 follow-up · P0): Metric-emission tests for
// GetSharedGameByIdQueryHandler.
//
// We use System.Diagnostics.Metrics.MeterListener to capture instrument values
// without going through Prometheus / OTLP — this keeps the test hermetic and
// avoids spinning up a metrics exporter just to assert an Add(1) call.
//
// Coverage matrix:
//   • Success path                → request=success,  source=origin (cache miss)
//   • Not-found path              → request=not_found, source=origin
//   • Repeated call (cache hit)   → request=success,  source=hit
//   • Cross-BC fan-out histograms → toolkit + agent + kb each emit one sample
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Observability;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetSharedGameByIdQueryHandlerMetricsTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock = new();
    private readonly Mock<ILogger<GetSharedGameByIdQueryHandler>> _loggerMock = new();

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private GetSharedGameByIdQueryHandler CreateHandler(MeepleAiDbContext db, HybridCache cache) =>
        new(_repositoryMock.Object, db, cache, CreateConfiguration(), _loggerMock.Object);

    private static SharedGame CreateGame() =>
        SharedGame.Create(
            "Catan",
            1995,
            "Description",
            3,
            4,
            90,
            10,
            2.5m,
            7.8m,
            "https://example.com/catan.jpg",
            "https://example.com/thumb.jpg",
            GameRules.Create("Rules content", "en"),
            createdBy: Guid.NewGuid(),
            bggId: 13);

    /// <summary>
    /// Captures every measurement emitted on the <c>MeepleAI.Api</c> meter
    /// during the lifetime of the listener. Only instruments whose name starts
    /// with <c>meepleai.shared_game_detail.</c> are captured to keep the test
    /// noise-free if the handler ever incidentally records other metrics.
    /// </summary>
    private sealed class SharedGameDetailMetricsCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public ConcurrentBag<(string Name, long Value, IReadOnlyDictionary<string, object?> Tags)> LongMeasurements { get; } = new();
        public ConcurrentBag<(string Name, double Value, IReadOnlyDictionary<string, object?> Tags)> DoubleMeasurements { get; } = new();

        public SharedGameDetailMetricsCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName &&
                        instrument.Name.StartsWith("meepleai.shared_game_detail.", StringComparison.Ordinal))
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                LongMeasurements.Add((instrument.Name, measurement, ToDict(tags)));
            });
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                DoubleMeasurements.Add((instrument.Name, measurement, ToDict(tags)));
            });

            _listener.Start();
        }

        private static IReadOnlyDictionary<string, object?> ToDict(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var d = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);
            foreach (var t in tags)
            {
                d[t.Key] = t.Value;
            }
            return d;
        }

        public void Dispose() => _listener.Dispose();
    }

    [Fact]
    public async Task Handle_SuccessOnCacheMiss_EmitsRequestSuccessAndOriginAndAllFanOutHistograms()
    {
        // Arrange
        using var capture = new SharedGameDetailMetricsCapture();
        var game = CreateGame();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db, CreateHybridCache());

        // Act
        var result = await handler.Handle(new GetSharedGameByIdQuery(game.Id), TestContext.Current.CancellationToken);

        // Assert — sanity on the result
        result.Should().NotBeNull();

        // RequestsTotal: exactly one increment with result=success.
        var requests = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.requests.total")
            .ToList();
        requests.Should().ContainSingle();
        requests[0].Value.Should().Be(1);
        requests[0].Tags["result"].Should().Be(MeepleAiMetrics.SharedGameDetailResults.Success);

        // CacheHitsTotal: source=origin because the factory ran (cache miss).
        var cacheHits = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.cache_hits.total")
            .ToList();
        cacheHits.Should().ContainSingle();
        cacheHits[0].Tags["source"].Should().Be(MeepleAiMetrics.CacheHitSources.Origin);

        // RenderDurationSeconds: one sample, cache_outcome=origin.
        var render = capture.DoubleMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.render_duration_seconds")
            .ToList();
        render.Should().ContainSingle();
        render[0].Value.Should().BeGreaterThanOrEqualTo(0d);
        render[0].Tags["cache_outcome"].Should().Be(MeepleAiMetrics.SharedGameDetailCacheOutcomes.Origin);

        // CrossBcQueryDurationSeconds: one sample for each fan-out (toolkit, agent, kb).
        var crossBc = capture.DoubleMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.cross_bc_query_duration_seconds")
            .ToList();
        crossBc.Should().HaveCount(3);
        var labels = crossBc.Select(m => (string?)m.Tags["bounded_context"]).ToList();
        labels.Should().BeEquivalentTo(new[]
        {
            MeepleAiMetrics.SharedGameDetailBoundedContexts.Toolkit,
            MeepleAiMetrics.SharedGameDetailBoundedContexts.Agent,
            MeepleAiMetrics.SharedGameDetailBoundedContexts.Kb
        });
        crossBc.Should().OnlyContain(m => m.Value >= 0d);
    }

    [Fact]
    public async Task Handle_NotFound_EmitsRequestNotFoundAndOrigin()
    {
        // Arrange
        using var capture = new SharedGameDetailMetricsCapture();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db, CreateHybridCache());

        // Act
        var result = await handler.Handle(new GetSharedGameByIdQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        var requests = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.requests.total")
            .ToList();
        requests.Should().ContainSingle();
        requests[0].Tags["result"].Should().Be(MeepleAiMetrics.SharedGameDetailResults.NotFound);

        // Cache miss still emits source=origin even when the underlying game is null —
        // the factory delegate runs before deciding the result. This matches the
        // metric semantics documented in MeepleAiMetrics.SharedGameDetail.cs.
        var cacheHits = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.cache_hits.total")
            .ToList();
        cacheHits.Should().ContainSingle();
        cacheHits[0].Tags["source"].Should().Be(MeepleAiMetrics.CacheHitSources.Origin);

        var render = capture.DoubleMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.render_duration_seconds")
            .ToList();
        render.Should().ContainSingle();
        render[0].Tags["cache_outcome"].Should().Be(MeepleAiMetrics.SharedGameDetailCacheOutcomes.Origin);
    }

    [Fact]
    public async Task Handle_RepeatedCall_SecondCallEmitsSourceHit()
    {
        // Arrange — share a single HybridCache between the two handler invocations.
        using var capture = new SharedGameDetailMetricsCapture();
        var game = CreateGame();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sharedCache = CreateHybridCache();
        var handler = CreateHandler(db, sharedCache);

        // Act — first call seeds cache (factoryInvoked=true ⇒ origin).
        await handler.Handle(new GetSharedGameByIdQuery(game.Id), TestContext.Current.CancellationToken);
        // Second call should hit the L1 entry (factoryInvoked=false ⇒ hit).
        await handler.Handle(new GetSharedGameByIdQuery(game.Id), TestContext.Current.CancellationToken);

        // Assert
        var cacheHitSources = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.cache_hits.total")
            .Select(m => (string?)m.Tags["source"])
            .ToList();
        cacheHitSources.Should().HaveCount(2);
        cacheHitSources.Should().Contain(MeepleAiMetrics.CacheHitSources.Origin);
        cacheHitSources.Should().Contain(MeepleAiMetrics.CacheHitSources.Hit);

        // Render histogram: first sample is origin, second is hit.
        var renderOutcomes = capture.DoubleMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.render_duration_seconds")
            .Select(m => (string?)m.Tags["cache_outcome"])
            .ToList();
        renderOutcomes.Should().HaveCount(2);
        renderOutcomes.Should().Contain(MeepleAiMetrics.SharedGameDetailCacheOutcomes.Origin);
        renderOutcomes.Should().Contain(MeepleAiMetrics.SharedGameDetailCacheOutcomes.Hit);

        // Cross-BC fan-out only runs on cache miss (inside the factory delegate),
        // so we should see exactly 3 samples (one per BC), not 6.
        var crossBc = capture.DoubleMeasurements
            .Where(m => m.Name == "meepleai.shared_game_detail.cross_bc_query_duration_seconds")
            .ToList();
        crossBc.Should().HaveCount(3);
    }
}
