using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Unit tests for <see cref="GetGameNightInvitationByTokenQueryHandler"/>.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// Verifies the public token-addressable projection, the
/// <c>AcceptedSoFar = 1 + accepted-invitations</c> formula, the
/// <c>AlreadyRespondedAs</c> mapping, and that responses are cached
/// per-token via <see cref="HybridCache"/> (perf gates §6).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GetGameNightInvitationByTokenQueryHandlerTests
{
    private const string Token = "abcdefghijklmnopqrstuv";
    private static readonly DateTimeOffset UtcNow = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightInvitationRepository> _invitationRepoMock = new();
    private readonly Mock<IGameNightEventRepository> _gameNightRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IGameRepository> _gameRepoMock = new();
    private readonly HybridCache _cache = CreateHybridCache();
    private readonly GetGameNightInvitationByTokenQueryHandler _sut;

    public GetGameNightInvitationByTokenQueryHandlerTests()
    {
        _sut = new GetGameNightInvitationByTokenQueryHandler(
            _invitationRepoMock.Object,
            _gameNightRepoMock.Object,
            _userRepoMock.Object,
            _gameRepoMock.Object,
            _cache);
    }

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static GameNightInvitation Reconstitute(GameNightInvitationStatus status)
    {
        return GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Token,
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            status: status,
            expiresAt: UtcNow.AddDays(7),
            respondedAt: status == GameNightInvitationStatus.Pending ? null : UtcNow,
            respondedByUserId: null,
            createdAt: UtcNow.AddDays(-1),
            createdBy: Guid.NewGuid());
    }

    private static GameNightEvent CreateGameNight(Guid organizerId, List<Guid>? gameIds = null)
    {
        return GameNightEvent.Create(
            organizerId: organizerId,
            title: "Friday Night Catan",
            scheduledAt: UtcNow.AddDays(7),
            description: null,
            location: "My place",
            maxPlayers: 5,
            gameIds: gameIds);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsNull()
    {
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightInvitation?)null);

        var result = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_GameNightMissing_ReturnsNull()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var result = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PendingInvitation_ProjectsAcceptedSoFarAsOrganizerPlusCount()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        var organizerId = Guid.NewGuid();
        var gameNight = CreateGameNight(organizerId);

        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _invitationRepoMock
            .Setup(r => r.CountAcceptedByGameNightIdAsync(
                invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Token.Should().Be(Token);
        result.Status.Should().Be(nameof(GameNightInvitationStatus.Pending));
        // Organizer always counted as attending → +1 to accepted invitations.
        result.AcceptedSoFar.Should().Be(4);
        result.AlreadyRespondedAs.Should().BeNull();
        result.HostDisplayName.Should().Be("A friend"); // Fallback when user is null.
        result.PrimaryGameId.Should().BeNull(); // No GameIds.
    }

    [Fact]
    public async Task Handle_AcceptedInvitation_MapsAlreadyRespondedAsAccepted()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Accepted);
        var gameNight = CreateGameNight(Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var result = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result.Should().NotBeNull();
        result!.AlreadyRespondedAs.Should().Be("Accepted");
    }

    [Fact]
    public async Task Handle_PrimaryGameLookup_PopulatesNameAndImageUrl()
    {
        var primaryGameId = Guid.NewGuid();
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        var gameNight = CreateGameNight(Guid.NewGuid(), gameIds: new List<Guid> { primaryGameId });
        var game = new Api.BoundedContexts.GameManagement.Domain.Entities.Game(
            id: primaryGameId,
            title: new GameTitle("Catan"));
        game.SetImages(iconUrl: null, imageUrl: "https://cdn.example/catan.jpg");

        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _gameRepoMock
            .Setup(r => r.GetByIdAsync(primaryGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var result = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result.Should().NotBeNull();
        result!.PrimaryGameId.Should().Be(primaryGameId);
        result.PrimaryGameName.Should().Be("Catan");
        result.PrimaryGameImageUrl.Should().Be("https://cdn.example/catan.jpg");
    }

    [Fact]
    public async Task Handle_RepeatedCalls_ServeFromCacheWithoutReinvokingRepository()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        var gameNight = CreateGameNight(Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var first = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);
        var second = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        // Second call MUST hit cache (L1) — repo factory invoked exactly once.
        _invitationRepoMock.Verify(
            r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
