using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;
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
public class GetDashboardQueryHandlerTests
{
    private readonly Mock<IMechanicAnalysisMetricsRepository> _metricsRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GetDashboardQueryHandler>> _loggerMock = new();

    private readonly GetDashboardQueryHandler _handler;

    public GetDashboardQueryHandlerTests()
    {
        _handler = new GetDashboardQueryHandler(
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
        var act = () => new GetDashboardQueryHandler(
            metricsRepository: null!,
            _cacheMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("metricsRepository");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new GetDashboardQueryHandler(
            _metricsRepoMock.Object,
            cache: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new GetDashboardQueryHandler(
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
    public async Task Handle_HappyPath_ReturnsRowsInRepositoryOrder()
    {
        var rows = new[]
        {
            new DashboardGameRow(
                Guid.NewGuid(),
                "Catan",
                CertificationStatus.Certified,
                0.92m,
                DateTimeOffset.UtcNow.AddHours(-1)),
            new DashboardGameRow(
                Guid.NewGuid(),
                "Wingspan",
                CertificationStatus.NotEvaluated,
                0.78m,
                DateTimeOffset.UtcNow.AddHours(-2)),
            new DashboardGameRow(
                Guid.NewGuid(),
                "Brass",
                CertificationStatus.NotCertified,
                0.41m,
                null),
        };

        _metricsRepoMock
            .Setup(r => r.GetDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().ContainInOrder(rows);
        _metricsRepoMock.Verify(r => r.GetDashboardAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryReturnsEmptyList_ReturnsEmptyListNotNull()
    {
        _metricsRepoMock
            .Setup(r => r.GetDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DashboardGameRow>());

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ============================================================================================
    // Cache fallback
    // ============================================================================================

    [Fact]
    public async Task Handle_WhenCacheThrows_FallsBackToDirectDbAndReturnsRows()
    {
        var rows = new[]
        {
            new DashboardGameRow(
                Guid.NewGuid(),
                "Terraforming Mars",
                CertificationStatus.Certified,
                0.88m,
                DateTimeOffset.UtcNow),
        };

        _metricsRepoMock
            .Setup(r => r.GetDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetDashboardQueryHandler.CachedDashboardResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache offline"));

        var result = await _handler.Handle(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().ContainInOrder(rows);

        // Cache was attempted exactly once.
        _cacheMock.Verify(
            c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetDashboardQueryHandler.CachedDashboardResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Repo was called exactly once via the fallback path (the failing factory never executed).
        _metricsRepoMock.Verify(r => r.GetDashboardAsync(It.IsAny<CancellationToken>()), Times.Once);

        // A warning was logged for the cache failure.
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ============================================================================================
    // Cache key + tags + TTL inspection
    // ============================================================================================

    [Fact]
    public async Task Handle_UsesExpectedCacheKeyTagsAndTtl()
    {
        _metricsRepoMock
            .Setup(r => r.GetDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DashboardGameRow>());

        string? capturedKey = null;
        string[]? capturedTags = null;
        TimeSpan? capturedTtl = null;

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetDashboardQueryHandler.CachedDashboardResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string key,
                Func<CancellationToken, Task<GetDashboardQueryHandler.CachedDashboardResult>> factory,
                string[]? tags,
                TimeSpan? ttl,
                CancellationToken ct) =>
            {
                capturedKey = key;
                capturedTags = tags;
                capturedTtl = ttl;
                return factory(ct);
            });

        await _handler.Handle(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        capturedKey.Should().Be("meepleai:mechanic-validation:dashboard");
        capturedTtl.Should().Be(TimeSpan.FromMinutes(5));
        capturedTags.Should().NotBeNull();
        capturedTags.Should().BeEquivalentTo(new[] { "mechanic-golden", "mechanic-validation-dashboard" });
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
                It.IsAny<Func<CancellationToken, Task<GetDashboardQueryHandler.CachedDashboardResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<GetDashboardQueryHandler.CachedDashboardResult>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));
    }
}
