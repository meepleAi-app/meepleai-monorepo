using Api.BoundedContexts.Administration.Application.Queries.Metrics;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.Metrics;

/// <summary>
/// Unit tests for <see cref="GetPrometheusMetricLabelsQueryHandler"/>.
/// Covers: live success, Prometheus offline fallback, cache hit reuse, empty response treated as fallback.
/// Issue #1840 SP5 F4-C7.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public class GetPrometheusMetricLabelsQueryHandlerTests
{
    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static GetPrometheusMetricLabelsQueryHandler CreateHandler(
        Mock<IPrometheusLabelsClient> clientMock) =>
        new(clientMock.Object, CreateHybridCache(), new Mock<ILogger<GetPrometheusMetricLabelsQueryHandler>>().Object);

    [Fact]
    public async Task Handle_WhenPrometheusReturnsLabels_ReturnsLiveResultMarkedAsNotFallback()
    {
        var clientMock = new Mock<IPrometheusLabelsClient>();
        clientMock
            .Setup(c => c.GetMetricNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "meepleai_chat_p95_ms", "up" });

        var handler = CreateHandler(clientMock);

        var result = await handler.Handle(new GetPrometheusMetricLabelsQuery(), CancellationToken.None);

        result.IsFallback.Should().BeFalse();
        result.Labels.Should().BeEquivalentTo(new[] { "meepleai_chat_p95_ms", "up" });
    }

    [Fact]
    public async Task Handle_WhenPrometheusOffline_ReturnsFallbackListMarked()
    {
        var clientMock = new Mock<IPrometheusLabelsClient>();
        clientMock
            .Setup(c => c.GetMetricNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>?)null);

        var handler = CreateHandler(clientMock);

        var result = await handler.Handle(new GetPrometheusMetricLabelsQuery(), CancellationToken.None);

        result.IsFallback.Should().BeTrue();
        result.Labels.Should().BeEquivalentTo(GetPrometheusMetricLabelsQueryHandler.FallbackLabels);
    }

    [Fact]
    public async Task Handle_WhenPrometheusReturnsEmptyList_ReturnsFallbackListMarked()
    {
        var clientMock = new Mock<IPrometheusLabelsClient>();
        clientMock
            .Setup(c => c.GetMetricNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var handler = CreateHandler(clientMock);

        var result = await handler.Handle(new GetPrometheusMetricLabelsQuery(), CancellationToken.None);

        result.IsFallback.Should().BeTrue();
        result.Labels.Should().BeEquivalentTo(GetPrometheusMetricLabelsQueryHandler.FallbackLabels);
    }

    [Fact]
    public async Task Handle_CallTwiceWithinTtl_ShouldHitCacheOnSecondCall()
    {
        var clientMock = new Mock<IPrometheusLabelsClient>();
        clientMock
            .Setup(c => c.GetMetricNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "metric_a", "metric_b" });

        var handler = CreateHandler(clientMock);

        // Call #1 — populates cache
        var first = await handler.Handle(new GetPrometheusMetricLabelsQuery(), CancellationToken.None);
        // Call #2 — should be served from cache (same handler/HybridCache instance)
        var second = await handler.Handle(new GetPrometheusMetricLabelsQuery(), CancellationToken.None);

        first.Labels.Should().BeEquivalentTo(second.Labels);
        clientMock.Verify(c => c.GetMetricNamesAsync(It.IsAny<CancellationToken>()), Times.Once,
            "HybridCache should serve subsequent calls within the 60s window from cache");
    }
}
