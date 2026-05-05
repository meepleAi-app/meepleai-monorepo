using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="GameNightInvitationRespondedHandler"/>.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// Verifies the post-RSVP fan-out:
/// (a) cache tag invalidation for <c>game-night-invitation:{token}</c>;
/// (b) Accepted-only confirmation email dispatch (declined guests skipped).
///
/// Uses raw <see cref="HybridCache"/> (NOT <c>IHybridCacheService</c>) to share
/// the native tag index populated by
/// <see cref="GetGameNightInvitationByTokenQueryHandler"/> (pitfall #2620).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightInvitationRespondedHandlerTests
{
    private const string Token = "abcdefghijklmnopqrstuv";
    private static readonly DateTimeOffset UtcNow = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightInvitationRepository> _invitationRepoMock = new();
    private readonly Mock<IGameNightEventRepository> _gameNightRepoMock = new();
    private readonly Mock<IGameNightEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<GameNightInvitationRespondedHandler>> _loggerMock = new();

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private GameNightInvitationRespondedHandler CreateHandler(HybridCache cache) => new(
        _invitationRepoMock.Object,
        _gameNightRepoMock.Object,
        _emailServiceMock.Object,
        cache,
        _loggerMock.Object);

    private static GameNightInvitation Reconstitute(GameNightInvitationStatus status)
    {
        return GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Token,
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            status: status,
            expiresAt: UtcNow.AddDays(7),
            respondedAt: UtcNow,
            respondedByUserId: null,
            createdAt: UtcNow.AddDays(-1),
            createdBy: Guid.NewGuid());
    }

    private static GameNightEvent CreateGameNight()
    {
        return GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Friday Night Catan",
            scheduledAt: UtcNow.AddDays(7),
            description: null,
            location: "My place",
            maxPlayers: 5,
            gameIds: null);
    }

    private static GameNightInvitationRespondedEvent CreateEvent(
        Guid invitationId, Guid gameNightId, GameNightInvitationStatus status)
    {
        return new GameNightInvitationRespondedEvent(
            gameNightInvitationId: invitationId,
            gameNightId: gameNightId,
            token: Token,
            status: status,
            respondedByUserId: null);
    }

    [Fact]
    public async Task Handle_AcceptedResponse_InvalidatesCacheAndSendsConfirmationEmail()
    {
        var cache = CreateHybridCache();
        var cacheTag = GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + Token;

        // Pre-seed cache entry tagged with the per-token tag → verify eviction.
        await cache.GetOrCreateAsync<string>(
            key: cacheTag,
            factory: _ => ValueTask.FromResult("seed"),
            options: null,
            tags: new[] { cacheTag },
            cancellationToken: CancellationToken.None);

        var invitation = Reconstitute(GameNightInvitationStatus.Accepted);
        var gameNight = CreateGameNight();
        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var notification = CreateEvent(
            invitation.Id, invitation.GameNightId, GameNightInvitationStatus.Accepted);

        await CreateHandler(cache).Handle(notification, CancellationToken.None);

        // Verify cache eviction: factory must run again on subsequent access.
        var factoryRan = false;
        await cache.GetOrCreateAsync<string>(
            key: cacheTag,
            factory: _ =>
            {
                factoryRan = true;
                return ValueTask.FromResult("repop");
            },
            options: null,
            tags: new[] { cacheTag },
            cancellationToken: CancellationToken.None);
        factoryRan.Should().BeTrue();

        // Verify confirmation email dispatched with token-bearing unsubscribe URL.
        _emailServiceMock.Verify(
            e => e.SendGameNightRsvpConfirmationEmailAsync(
                "guest@example.com",
                gameNight.Title,
                gameNight.ScheduledAt,
                gameNight.Location,
                $"/invites/{Token}/unsubscribe",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeclinedResponse_InvalidatesCacheButSkipsEmail()
    {
        var cache = CreateHybridCache();

        var notification = CreateEvent(
            Guid.NewGuid(), Guid.NewGuid(), GameNightInvitationStatus.Declined);

        await CreateHandler(cache).Handle(notification, CancellationToken.None);

        // Declined responses must NOT trigger confirmation email and must NOT
        // round-trip the invitation/game-night repositories.
        _emailServiceMock.Verify(
            e => e.SendGameNightRsvpConfirmationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _invitationRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gameNightRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AcceptedButInvitationDisappeared_SkipsEmailGracefully()
    {
        // Race condition: aggregate purged between event raise and handler.
        var cache = CreateHybridCache();
        var invitationId = Guid.NewGuid();
        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightInvitation?)null);

        var notification = CreateEvent(
            invitationId, Guid.NewGuid(), GameNightInvitationStatus.Accepted);

        var act = async () => await CreateHandler(cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _emailServiceMock.Verify(
            e => e.SendGameNightRsvpConfirmationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AcceptedButGameNightDisappeared_SkipsEmailGracefully()
    {
        var cache = CreateHybridCache();
        var invitation = Reconstitute(GameNightInvitationStatus.Accepted);
        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var notification = CreateEvent(
            invitation.Id, invitation.GameNightId, GameNightInvitationStatus.Accepted);

        var act = async () => await CreateHandler(cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _emailServiceMock.Verify(
            e => e.SendGameNightRsvpConfirmationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
