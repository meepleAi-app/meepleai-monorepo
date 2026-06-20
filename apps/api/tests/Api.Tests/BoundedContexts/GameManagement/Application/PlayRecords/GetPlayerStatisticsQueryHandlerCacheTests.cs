using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for the HybridCache wrapping added to <see cref="GetPlayerStatisticsQueryHandler"/>
/// in #2438 (PR-B). Verifies the handler delegates to <see cref="IHybridCacheService.GetOrCreateAsync"/>
/// with the per-user/per-range key, the per-user tag, and the 5-minute TTL (ADR-081).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2438")]
public sealed class GetPlayerStatisticsQueryHandlerCacheTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly Mock<IHybridCacheService> _cache;

    public GetPlayerStatisticsQueryHandlerCacheTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _cache = new Mock<IHybridCacheService>();

        // Pass-through: invoke the factory and return its value so the wrapped compute runs.
        _cache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<PlayerStatisticsDto>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<PlayerStatisticsDto>>, string[], TimeSpan?, CancellationToken>(
                (key, factory, tags, exp, ct) => factory(ct));
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Handle_NoDateRange_UsesPerUserKeyTagAndFiveMinuteTtl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = new GetPlayerStatisticsQueryHandler(_context, _cache.Object);

        // Act
        await handler.Handle(
            new GetPlayerStatisticsQuery(userId),
            TestContext.Current.CancellationToken);

        // Assert — key is player-stats:{userId}:0:0 (null start/end → Ticks 0),
        // tag is player-stats:{userId}, TTL is 5 minutes.
        _cache.Verify(c => c.GetOrCreateAsync(
            It.Is<string>(k => k == $"player-stats:{userId}:0:0"),
            It.IsAny<Func<CancellationToken, Task<PlayerStatisticsDto>>>(),
            It.Is<string[]>(t => t.Contains($"player-stats:{userId}")),
            It.Is<TimeSpan?>(e => e == TimeSpan.FromMinutes(5)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDateRange_EncodesTicksInKeyAndKeepsSamePerUserTag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var handler = new GetPlayerStatisticsQueryHandler(_context, _cache.Object);

        // Act
        await handler.Handle(
            new GetPlayerStatisticsQuery(userId, start, end),
            TestContext.Current.CancellationToken);

        // Assert — the range Ticks are part of the key; the tag stays per-user (range-agnostic)
        // so a single eviction clears every ranged entry for the user.
        _cache.Verify(c => c.GetOrCreateAsync(
            It.Is<string>(k => k == $"player-stats:{userId}:{start.Ticks}:{end.Ticks}"),
            It.IsAny<Func<CancellationToken, Task<PlayerStatisticsDto>>>(),
            It.Is<string[]>(t => t.Contains($"player-stats:{userId}")),
            It.Is<TimeSpan?>(e => e == TimeSpan.FromMinutes(5)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetPlayerStatisticsQueryHandler(_context, _cache.Object);

        // Act
        var act = () => handler.Handle(null!, TestContext.Current.CancellationToken);

        // Assert — null-guard runs before any cache interaction.
        await act.Should().ThrowAsync<ArgumentNullException>();
        _cache.Verify(c => c.GetOrCreateAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task<PlayerStatisticsDto>>>(),
            It.IsAny<string[]>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
