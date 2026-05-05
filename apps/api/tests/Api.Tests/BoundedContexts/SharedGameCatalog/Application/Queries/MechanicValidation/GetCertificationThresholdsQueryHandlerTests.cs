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
public class GetCertificationThresholdsQueryHandlerTests
{
    private readonly Mock<ICertificationThresholdsConfigRepository> _configRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GetCertificationThresholdsQueryHandler>> _loggerMock = new();

    private readonly GetCertificationThresholdsQueryHandler _handler;

    public GetCertificationThresholdsQueryHandlerTests()
    {
        _handler = new GetCertificationThresholdsQueryHandler(
            _configRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullConfigRepository_Throws()
    {
        var act = () => new GetCertificationThresholdsQueryHandler(
            configRepository: null!,
            _cacheMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configRepository");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new GetCertificationThresholdsQueryHandler(
            _configRepoMock.Object,
            cache: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new GetCertificationThresholdsQueryHandler(
            _configRepoMock.Object,
            _cacheMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ============================================================================================
    // Cache hit / miss behavior
    // ============================================================================================

    [Fact]
    public async Task Handle_OnCacheHit_ReturnsCachedAndDoesNotHitRepo()
    {
        var cachedThresholds = CertificationThresholds.Create(
            minCoveragePct: 75m,
            maxPageTolerance: 5,
            minBggMatchPct: 82m,
            minOverallScore: 65m);

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetCertificationThresholdsQueryHandler.CachedThresholdsResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetCertificationThresholdsQueryHandler.CachedThresholdsResult(cachedThresholds));

        var result = await _handler.Handle(
            new GetCertificationThresholdsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(cachedThresholds);

        _configRepoMock.Verify(
            r => r.GetAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OnCacheMiss_LoadsFromRepoAndCachesWithCorrectKeyAndTags()
    {
        var thresholds = CertificationThresholds.Default();
        var config = CertificationThresholdsConfig.Reconstitute(
            thresholds,
            updatedAt: DateTimeOffset.UtcNow,
            updatedByUserId: null);

        _configRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        string? capturedKey = null;
        string[]? capturedTags = null;
        TimeSpan? capturedTtl = null;

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetCertificationThresholdsQueryHandler.CachedThresholdsResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string key,
                Func<CancellationToken, Task<GetCertificationThresholdsQueryHandler.CachedThresholdsResult>> factory,
                string[]? tags,
                TimeSpan? ttl,
                CancellationToken ct) =>
            {
                capturedKey = key;
                capturedTags = tags;
                capturedTtl = ttl;
                return factory(ct);
            });

        var result = await _handler.Handle(
            new GetCertificationThresholdsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(thresholds);

        capturedKey.Should().Be("meepleai:mechanic-validation:thresholds");
        capturedTtl.Should().Be(TimeSpan.FromMinutes(30));
        capturedTags.Should().NotBeNull();
        capturedTags.Should().BeEquivalentTo(new[] { "mechanic-validation-thresholds" });

        _configRepoMock.Verify(
            r => r.GetAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================================================
    // Cache fallback
    // ============================================================================================

    [Fact]
    public async Task Handle_OnCacheException_FallsBackToRepoLoad()
    {
        var thresholds = CertificationThresholds.Default();
        var config = CertificationThresholdsConfig.Reconstitute(
            thresholds,
            updatedAt: DateTimeOffset.UtcNow,
            updatedByUserId: null);

        _configRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var cacheException = new InvalidOperationException("cache offline");

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetCertificationThresholdsQueryHandler.CachedThresholdsResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(cacheException);

        var result = await _handler.Handle(
            new GetCertificationThresholdsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(thresholds);

        // Cache was attempted exactly once.
        _cacheMock.Verify(
            c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetCertificationThresholdsQueryHandler.CachedThresholdsResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Repo was called exactly once via the fallback path.
        _configRepoMock.Verify(
            r => r.GetAsync(It.IsAny<CancellationToken>()),
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
    // Happy path: pulls Thresholds property off the loaded config aggregate
    // ============================================================================================

    [Fact]
    public async Task Handle_ReturnsThresholdsFromConfigAggregate()
    {
        var expected = CertificationThresholds.Create(
            minCoveragePct: 88m,
            maxPageTolerance: 3,
            minBggMatchPct: 91m,
            minOverallScore: 72m);

        var config = CertificationThresholdsConfig.Reconstitute(
            expected,
            updatedAt: DateTimeOffset.UtcNow,
            updatedByUserId: Guid.NewGuid());

        _configRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(
            new GetCertificationThresholdsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        result.MinCoveragePct.Should().Be(88m);
        result.MaxPageTolerance.Should().Be(3);
        result.MinBggMatchPct.Should().Be(91m);
        result.MinOverallScore.Should().Be(72m);

        _configRepoMock.Verify(
            r => r.GetAsync(It.IsAny<CancellationToken>()),
            Times.Once);
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
                It.IsAny<Func<CancellationToken, Task<GetCertificationThresholdsQueryHandler.CachedThresholdsResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<GetCertificationThresholdsQueryHandler.CachedThresholdsResult>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));
    }
}
