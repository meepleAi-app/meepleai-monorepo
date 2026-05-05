using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="SessionCompletedForContributorsHandler"/>.
/// Issue #593 (Wave A.3a) — spec §6.5.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SessionCompletedForContributorsHandlerTests
{
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<SessionCompletedForContributorsHandler>> _loggerMock = new();

    private SessionCompletedForContributorsHandler CreateHandler() =>
        new(_cacheMock.Object, _loggerMock.Object);

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionCompletedForContributorsHandler(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionCompletedForContributorsHandler(_cacheMock.Object, null!));
    }

    [Fact]
    public async Task Handle_GameSessionCompletedEvent_InvalidatesTopContributorsTag()
    {
        var notification = new GameSessionCompletedEvent(
            sessionId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            completedAt: DateTime.UtcNow,
            duration: TimeSpan.FromMinutes(45));

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync(GetTopContributorsQueryHandler.CacheTag, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GameCompletedInNightEvent_WithWinner_InvalidatesTopContributorsTag()
    {
        var notification = new GameCompletedInNightEvent(
            GameNightId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            GameTitle: "Catan",
            WinnerId: Guid.NewGuid());

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync(GetTopContributorsQueryHandler.CacheTag, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GameCompletedInNightEvent_WithoutWinner_InvalidatesTopContributorsTag()
    {
        var notification = new GameCompletedInNightEvent(
            GameNightId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            GameTitle: "Catan",
            WinnerId: null);

        await CreateHandler().Handle(notification, CancellationToken.None);

        _cacheMock.Verify(
            c => c.RemoveByTagAsync(GetTopContributorsQueryHandler.CacheTag, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullGameSessionCompletedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((GameSessionCompletedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullGameCompletedInNightEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler().Handle((GameCompletedInNightEvent)null!, CancellationToken.None));
    }
}
