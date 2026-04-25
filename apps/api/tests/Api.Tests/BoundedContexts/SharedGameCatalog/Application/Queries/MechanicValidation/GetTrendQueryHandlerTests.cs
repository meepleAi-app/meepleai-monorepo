using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetTrendQueryHandlerTests
{
    private readonly Mock<IMechanicAnalysisMetricsRepository> _metricsRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GetTrendQueryHandler>> _loggerMock = new();

    private readonly GetTrendQueryHandler _handler;

    public GetTrendQueryHandlerTests()
    {
        _handler = new GetTrendQueryHandler(
            _metricsRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullMetricsRepository_Throws()
    {
        var act = () => new GetTrendQueryHandler(
            metricsRepository: null!,
            _cacheMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("metricsRepository");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new GetTrendQueryHandler(
            _metricsRepoMock.Object,
            cache: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new GetTrendQueryHandler(
            _metricsRepoMock.Object,
            _cacheMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ============================================================================================
    // Handle argument-guard tests
    // ============================================================================================

    [Fact]
    public async Task Handle_WithNullRequest_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    // ============================================================================================
    // Happy path
    // ============================================================================================

    [Fact]
    public async Task Handle_HappyPath_ReturnsRepoResultAndCachesIt()
    {
        var sharedGameId = Guid.NewGuid();
        const int take = 15;

        var snapshots = new[]
        {
            CreateMetrics(sharedGameId, coverage: 90, page: 95, bgg: 88),
            CreateMetrics(sharedGameId, coverage: 85, page: 92, bgg: 80),
            CreateMetrics(sharedGameId, coverage: 70, page: 80, bgg: 75),
        };

        _metricsRepoMock
            .Setup(r => r.GetTrendAsync(sharedGameId, take, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(
            new GetTrendQuery(sharedGameId, take),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().ContainInOrder(snapshots);

        _metricsRepoMock.Verify(
            r => r.GetTrendAsync(sharedGameId, take, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepoReturnsEmpty_ReturnsEmptyList()
    {
        var sharedGameId = Guid.NewGuid();

        _metricsRepoMock
            .Setup(r => r.GetTrendAsync(sharedGameId, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicAnalysisMetrics>());

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(
            new GetTrendQuery(sharedGameId),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _metricsRepoMock.Verify(
            r => r.GetTrendAsync(sharedGameId, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================================================
    // Cache fallback
    // ============================================================================================

    [Fact]
    public async Task Handle_WhenCacheThrows_FallsBackToRepoAndLogsWarning()
    {
        var sharedGameId = Guid.NewGuid();
        const int take = 25;

        var snapshots = new[]
        {
            CreateMetrics(sharedGameId, coverage: 92, page: 90, bgg: 88),
        };

        _metricsRepoMock
            .Setup(r => r.GetTrendAsync(sharedGameId, take, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        var cacheException = new InvalidOperationException("cache offline");

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetTrendQueryHandler.CachedTrendResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(cacheException);

        var result = await _handler.Handle(
            new GetTrendQuery(sharedGameId, take),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().ContainInOrder(snapshots);

        // Cache was attempted exactly once.
        _cacheMock.Verify(
            c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetTrendQueryHandler.CachedTrendResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Repo was called exactly once via the fallback path with the exact parameters.
        _metricsRepoMock.Verify(
            r => r.GetTrendAsync(sharedGameId, take, It.IsAny<CancellationToken>()),
            Times.Once);

        // The thrown exception was forwarded to LogWarning.
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                cacheException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ============================================================================================
    // Cache key + tags + TTL inspection
    // ============================================================================================

    [Fact]
    public async Task Handle_BuildsExpectedCacheKey_Tags_AndTtl()
    {
        var sharedGameId = Guid.NewGuid();
        const int take = 42;

        _metricsRepoMock
            .Setup(r => r.GetTrendAsync(sharedGameId, take, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicAnalysisMetrics>());

        string? capturedKey = null;
        string[]? capturedTags = null;
        TimeSpan? capturedTtl = null;

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetTrendQueryHandler.CachedTrendResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string key,
                Func<CancellationToken, Task<GetTrendQueryHandler.CachedTrendResult>> factory,
                string[]? tags,
                TimeSpan? ttl,
                CancellationToken ct) =>
            {
                capturedKey = key;
                capturedTags = tags;
                capturedTtl = ttl;
                return factory(ct);
            });

        await _handler.Handle(
            new GetTrendQuery(sharedGameId, take),
            TestContext.Current.CancellationToken);

        capturedKey.Should().Be($"meepleai:mechanic-validation:trend:{sharedGameId}:{take}");
        capturedTtl.Should().Be(TimeSpan.FromMinutes(5));
        capturedTags.Should().NotBeNull();
        capturedTags.Should().BeEquivalentTo(new[] { $"game:{sharedGameId}", "mechanic-validation-trend" });
    }

    // ============================================================================================
    // Helpers
    // ============================================================================================

    /// <summary>
    /// Configures the cache mock to simply invoke the factory delegate the handler passes in,
    /// returning the factory's result. This effectively bypasses caching while still exercising
    /// the handler's "happy path" (cache-populated-from-factory) code branch.
    /// </summary>
    private void SetupCacheToInvokeFactory()
    {
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetTrendQueryHandler.CachedTrendResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<GetTrendQueryHandler.CachedTrendResult>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));
    }

    private static MechanicAnalysisMetrics CreateMetrics(
        Guid sharedGameId,
        decimal coverage,
        decimal page,
        decimal bgg)
    {
        return MechanicAnalysisMetrics.Create(
            analysisId: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            coveragePct: coverage,
            pageAccuracyPct: page,
            bggMatchPct: bgg,
            thresholds: CertificationThresholds.Default(),
            goldenVersionHash: new string('a', 64),
            matchDetailsJson: "{}");
    }
}
