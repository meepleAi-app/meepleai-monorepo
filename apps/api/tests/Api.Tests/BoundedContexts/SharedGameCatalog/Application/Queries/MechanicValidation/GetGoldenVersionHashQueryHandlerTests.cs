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
public class GetGoldenVersionHashQueryHandlerTests
{
    private readonly Mock<IMechanicGoldenClaimRepository> _claimRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GetGoldenVersionHashQueryHandler>> _loggerMock = new();

    private readonly GetGoldenVersionHashQueryHandler _handler;

    public GetGoldenVersionHashQueryHandlerTests()
    {
        _handler = new GetGoldenVersionHashQueryHandler(
            _claimRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullClaimRepository_Throws()
    {
        var act = () => new GetGoldenVersionHashQueryHandler(
            claimRepository: null!,
            _cacheMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("claimRepository");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new GetGoldenVersionHashQueryHandler(
            _claimRepoMock.Object,
            cache: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new GetGoldenVersionHashQueryHandler(
            _claimRepoMock.Object,
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
    public async Task Handle_HappyPath_ReturnsHashValue_WhenRepositoryReturnsVersionHash()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenVersionHashQuery(sharedGameId);

        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionHash("abc123"));

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().Be("abc123");
        _claimRepoMock.Verify(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVersionHashIsNull_ReturnsEmptyString()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenVersionHashQuery(sharedGameId);

        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionHash?)null);

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().Be(string.Empty);
    }

    // ============================================================================================
    // Cache fallback
    // ============================================================================================

    [Fact]
    public async Task Handle_WhenCacheThrows_FallsBackToDirectDbAndReturnsHash()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenVersionHashQuery(sharedGameId);

        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionHash("hash-after-fallback"));

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetGoldenVersionHashQueryHandler.CachedVersionHashResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache offline"));

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().Be("hash-after-fallback");

        // Cache was attempted exactly once.
        _cacheMock.Verify(
            c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetGoldenVersionHashQueryHandler.CachedVersionHashResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Repo was called exactly once via the fallback path (the failing factory never executed).
        _claimRepoMock.Verify(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()), Times.Once);

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
    // Cache key + TTL inspection
    // ============================================================================================

    [Fact]
    public async Task Handle_UsesExpectedCacheKeyAndTtl()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenVersionHashQuery(sharedGameId);

        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionHash?)null);

        string? capturedKey = null;
        string[]? capturedTags = null;
        TimeSpan? capturedTtl = null;

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetGoldenVersionHashQueryHandler.CachedVersionHashResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string key,
                Func<CancellationToken, Task<GetGoldenVersionHashQueryHandler.CachedVersionHashResult>> factory,
                string[]? tags,
                TimeSpan? ttl,
                CancellationToken ct) =>
            {
                capturedKey = key;
                capturedTags = tags;
                capturedTtl = ttl;
                return factory(ct);
            });

        await _handler.Handle(query, TestContext.Current.CancellationToken);

        capturedKey.Should().Be($"meepleai:mechanic-golden:version-hash:{sharedGameId}");
        capturedTtl.Should().Be(TimeSpan.FromMinutes(10));
        capturedTags.Should().NotBeNull();
        capturedTags.Should().Contain($"game:{sharedGameId}");
        capturedTags.Should().Contain("mechanic-golden");
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
                It.IsAny<Func<CancellationToken, Task<GetGoldenVersionHashQueryHandler.CachedVersionHashResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<GetGoldenVersionHashQueryHandler.CachedVersionHashResult>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));
    }
}
