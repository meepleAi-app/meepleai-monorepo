using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
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
public class GetGoldenForGameQueryHandlerTests
{
    private readonly Mock<IMechanicGoldenClaimRepository> _claimRepoMock = new();
    private readonly Mock<IMechanicGoldenBggTagRepository> _tagRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GetGoldenForGameQueryHandler>> _loggerMock = new();

    private readonly GetGoldenForGameQueryHandler _handler;

    public GetGoldenForGameQueryHandlerTests()
    {
        _handler = new GetGoldenForGameQueryHandler(
            _claimRepoMock.Object,
            _tagRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullClaimRepository_Throws()
    {
        var act = () => new GetGoldenForGameQueryHandler(
            claimRepository: null!,
            _tagRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("claimRepository");
    }

    [Fact]
    public void Constructor_WithNullTagRepository_Throws()
    {
        var act = () => new GetGoldenForGameQueryHandler(
            _claimRepoMock.Object,
            tagRepository: null!,
            _cacheMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tagRepository");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new GetGoldenForGameQueryHandler(
            _claimRepoMock.Object,
            _tagRepoMock.Object,
            cache: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new GetGoldenForGameQueryHandler(
            _claimRepoMock.Object,
            _tagRepoMock.Object,
            _cacheMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ============================================================================================
    // Handle argument-guard tests
    // ============================================================================================

    [Fact]
    public async Task Handle_WithNullQuery_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("query");
    }

    // ============================================================================================
    // Happy path
    // ============================================================================================

    [Fact]
    public async Task Handle_HappyPath_ReturnsDto_WhenCachePopulatesFromRepositories()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenForGameQuery(sharedGameId);

        var claim1 = BuildClaim(sharedGameId, MechanicSection.Mechanics, "Players take turns rolling dice.", expectedPage: 4);
        var claim2 = BuildClaim(sharedGameId, MechanicSection.Victory, "First to 10 points wins.", expectedPage: 12);
        var tag = MechanicGoldenBggTag.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            name: "Dice Rolling",
            category: "Mechanism",
            importedAt: DateTimeOffset.UtcNow);

        _claimRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { claim1, claim2 });
        _tagRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tag });
        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionHash("abc123"));

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.SharedGameId.Should().Be(sharedGameId);
        result.VersionHash.Should().Be("abc123");

        result.Claims.Should().HaveCount(2);
        var first = result.Claims[0];
        first.Id.Should().Be(claim1.Id);
        first.Section.Should().Be(MechanicSection.Mechanics);
        first.Statement.Should().Be(claim1.Statement);
        first.ExpectedPage.Should().Be(claim1.ExpectedPage);
        first.SourceQuote.Should().Be(claim1.SourceQuote);
        first.Keywords.Should().BeSameAs(claim1.Keywords);
        first.CreatedAt.Should().Be(claim1.CreatedAt);

        result.Claims[1].Id.Should().Be(claim2.Id);
        result.Claims[1].Section.Should().Be(MechanicSection.Victory);

        result.BggTags.Should().HaveCount(1);
        result.BggTags[0].Name.Should().Be("Dice Rolling");
        result.BggTags[0].Category.Should().Be("Mechanism");
    }

    [Fact]
    public async Task Handle_WhenVersionHashIsNull_ReturnsEmptyString()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenForGameQuery(sharedGameId);

        _claimRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicGoldenClaim>());
        _tagRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicGoldenBggTag>());
        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionHash?)null);

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.VersionHash.Should().Be(string.Empty);
    }

    [Fact]
    public async Task Handle_EmptyGoldenSet_ReturnsDtoWithEmptyCollections()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenForGameQuery(sharedGameId);

        _claimRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicGoldenClaim>());
        _tagRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicGoldenBggTag>());
        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionHash?)null);

        SetupCacheToInvokeFactory();

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.SharedGameId.Should().Be(sharedGameId);
        result.VersionHash.Should().Be(string.Empty);
        result.Claims.Should().BeEmpty();
        result.BggTags.Should().BeEmpty();
    }

    // ============================================================================================
    // Cache fallback
    // ============================================================================================

    [Fact]
    public async Task Handle_WhenCacheThrows_FallsBackToDirectDbAndReturnsDto()
    {
        var sharedGameId = Guid.NewGuid();
        var query = new GetGoldenForGameQuery(sharedGameId);

        var claim = BuildClaim(sharedGameId, MechanicSection.Phases, "Each round has three phases.", expectedPage: 7);

        _claimRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { claim });
        _tagRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicGoldenBggTag>());
        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionHash("hash-after-fallback"));

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetGoldenForGameQueryHandler.CachedGoldenForGameResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache offline"));

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.SharedGameId.Should().Be(sharedGameId);
        result.VersionHash.Should().Be("hash-after-fallback");
        result.Claims.Should().HaveCount(1);
        result.Claims[0].Statement.Should().Be(claim.Statement);

        // Cache was attempted exactly once.
        _cacheMock.Verify(
            c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetGoldenForGameQueryHandler.CachedGoldenForGameResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Repos were called exactly once via the fallback path (the failing factory never executed).
        _claimRepoMock.Verify(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()), Times.Once);
        _tagRepoMock.Verify(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()), Times.Once);
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
        var query = new GetGoldenForGameQuery(sharedGameId);

        _claimRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicGoldenClaim>());
        _tagRepoMock
            .Setup(r => r.GetByGameAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MechanicGoldenBggTag>());
        _claimRepoMock
            .Setup(r => r.GetVersionHashAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionHash?)null);

        string? capturedKey = null;
        string[]? capturedTags = null;
        TimeSpan? capturedTtl = null;

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetGoldenForGameQueryHandler.CachedGoldenForGameResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string key,
                Func<CancellationToken, Task<GetGoldenForGameQueryHandler.CachedGoldenForGameResult>> factory,
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

        capturedKey.Should().Be($"meepleai:mechanic-golden:{sharedGameId}");
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
                It.IsAny<Func<CancellationToken, Task<GetGoldenForGameQueryHandler.CachedGoldenForGameResult>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<GetGoldenForGameQueryHandler.CachedGoldenForGameResult>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));
    }

    private static MechanicGoldenClaim BuildClaim(Guid sharedGameId, MechanicSection section, string statement, int expectedPage)
    {
        return MechanicGoldenClaim.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            section: section,
            statement: statement,
            expectedPage: expectedPage,
            sourceQuote: "Source: rulebook page " + expectedPage,
            keywords: new[] { "kw1", "kw2" },
            embedding: null,
            curatorUserId: Guid.NewGuid(),
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            updatedAt: DateTimeOffset.UtcNow,
            deletedAt: null);
    }
}
