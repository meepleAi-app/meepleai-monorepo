using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Unit tests for <see cref="GetGameNightInvitationByTokenQueryHandler"/>.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// Verifies the public token-addressable projection, the
/// <c>AcceptedSoFar = 1 + accepted members + accepted-invitations</c> formula, the
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
    private readonly Mock<IGameCoreDataProvider> _gameCoreDataMock = new();
    private readonly HybridCache _cache = CreateHybridCache();
    private readonly FakeTimeProvider _timeProvider = new(UtcNow);
    private readonly GetGameNightInvitationByTokenQueryHandler _sut;

    public GetGameNightInvitationByTokenQueryHandlerTests()
    {
        _sut = new GetGameNightInvitationByTokenQueryHandler(
            _invitationRepoMock.Object,
            _gameNightRepoMock.Object,
            _userRepoMock.Object,
            _gameCoreDataMock.Object,
            _cache,
            _timeProvider);
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

    private static GameNightInvitation Reconstitute(GameNightInvitationStatus status, DateTimeOffset? expiresAt = null)
    {
        return GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Token,
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            status: status,
            expiresAt: expiresAt ?? UtcNow.AddDays(7),
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
    public async Task Handle_RegisteredMemberAccepted_CountsInAcceptedSoFar()
    {
        // #4706: the guest page counted the organizer and the emailed guests but not the
        // registered members who accepted, while the organizer's view counted only the
        // members — the two halves disagreed. Both now count the same set.
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        var organizerId = Guid.NewGuid();
        var acceptedMember = Guid.NewGuid();
        var pendingMember = Guid.NewGuid();
        var gameNight = CreateGameNight(organizerId);
        gameNight.Publish([acceptedMember, pendingMember]);
        gameNight.GetRsvp(acceptedMember)!.Accept();

        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _invitationRepoMock
            .Setup(r => r.CountAcceptedByGameNightIdAsync(
                invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        // organizer (1) + accepted member (1) + accepted emailed guests (2); the pending member is not.
        result!.AcceptedSoFar.Should().Be(4);
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
        var coreData = GameCoreData.Create(
            title: "Catan",
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            imageUrl: "https://cdn.example/catan.jpg");

        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _gameCoreDataMock
            .Setup(r => r.GetCoreDataAsync(GameRef.Shared(primaryGameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coreData);

        var result = await _sut.Handle(
            new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result.Should().NotBeNull();
        result!.PrimaryGameId.Should().Be(primaryGameId);
        result.PrimaryGameName.Should().Be("Catan");
        result.PrimaryGameImageUrl.Should().Be("https://cdn.example/catan.jpg");
    }

    // ── A link that is no longer alive shows nothing of the night (#4745, #4856) ────────────────────────────────

    private void GivenTheNight(GameNightInvitation invitation, GameNightEvent night)
    {
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(night.OrganizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithDisplayName("Marta").Build());
    }

    private static GameNightEvent NightWithAGame(Guid primaryGameId) =>
        CreateGameNight(Guid.NewGuid(), gameIds: new List<Guid> { primaryGameId });

    private static void AssertNothingOfTheNightIsShown(PublicGameNightInvitationDto dto)
    {
        dto.Title.Should().BeEmpty();
        dto.Location.Should().BeNull();
        dto.ScheduledAt.Should().Be(default(DateTimeOffset), "the time of the night is not shown");
        dto.DurationMinutes.Should().BeNull();
        dto.ExpectedPlayers.Should().Be(0);
        dto.AcceptedSoFar.Should().Be(0);
        dto.PrimaryGameId.Should().BeNull();
        dto.PrimaryGameName.Should().BeNull();
        dto.PrimaryGameImageUrl.Should().BeNull();
        dto.HostUserId.Should().Be(Guid.Empty);
        dto.HostAvatarUrl.Should().BeNull();
        dto.GameNightId.Should().Be(Guid.Empty);
        dto.AlreadyRespondedAs.Should().BeNull();
        dto.RespondedByName.Should().BeNull();
        dto.RespondedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Cancelled)]
    [InlineData(GameNightInvitationStatus.Expired)]
    public async Task Handle_AnInvitationThatIsNoLongerAlive_ShowsOnlyItsStateAndTheHost(GameNightInvitationStatus status)
    {
        var invitation = Reconstitute(status);
        GivenTheNight(invitation, NightWithAGame(Guid.NewGuid()));

        var result = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(status.ToString());
        result.HostDisplayName.Should().Be("Marta", "the page says «ask {host} to send it again»");
        result.Token.Should().Be(Token);
        result.ExpiresAt.Should().Be(invitation.ExpiresAt, "the invitation's own expiry is not about the night");
        result.NightCancelled.Should().BeFalse("the night is on: it is the invitation that is not");
        AssertNothingOfTheNightIsShown(result);
    }

    [Fact]
    public async Task Handle_AnInvitationThatIsNoLongerAlive_DoesNotEvenLookTheGameOrTheCountUp()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Cancelled);
        var primaryGameId = Guid.NewGuid();
        GivenTheNight(invitation, NightWithAGame(primaryGameId));

        await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        _gameCoreDataMock.Verify(
            g => g.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitationRepoMock.Verify(
            r => r.CountAcceptedByGameNightIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_APendingInvitationPastItsCutoff_IsTreatedAsExpired_AndShowsNothingOfTheNight()
    {
        // The respond endpoint already answers 410 for it; the page must not be more generous than that.
        var invitation = Reconstitute(GameNightInvitationStatus.Pending, expiresAt: UtcNow.AddMinutes(-1));
        GivenTheNight(invitation, NightWithAGame(Guid.NewGuid()));

        var result = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result!.Status.Should().Be("Expired");
        AssertNothingOfTheNightIsShown(result);
    }

    [Fact]
    public async Task Handle_APendingInvitationThatExpiresRightNow_IsAlreadyExpired()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending, expiresAt: UtcNow);
        GivenTheNight(invitation, NightWithAGame(Guid.NewGuid()));

        var result = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result!.Status.Should().Be("Expired");
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Pending)]
    [InlineData(GameNightInvitationStatus.Accepted)]
    [InlineData(GameNightInvitationStatus.Declined)]
    public async Task Handle_AnInvitationOfACancelledNight_SaysSo_AndShowsNothingOfTheNight(GameNightInvitationStatus status)
    {
        var invitation = Reconstitute(status);
        var night = NightWithAGame(Guid.NewGuid());
        night.Cancel();
        GivenTheNight(invitation, night);

        var result = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result!.NightCancelled.Should().BeTrue();
        result.Status.Should().Be(status.ToString(), "the invitation keeps its own state: it is the night that was cancelled");
        result.HostDisplayName.Should().Be("Marta");
        AssertNothingOfTheNightIsShown(result);
    }

    [Fact]
    public async Task Handle_ALiveInvitationOfAPublishedNight_IsShownAsBefore_AndTheNightIsNotCancelled()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        var night = CreateGameNight(Guid.NewGuid());
        night.Publish([]);
        GivenTheNight(invitation, night);

        var result = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result!.NightCancelled.Should().BeFalse();
        result.Title.Should().Be("Friday Night Catan");
        result.Location.Should().Be("My place");
        result.ScheduledAt.Should().Be(night.ScheduledAt);
        result.GameNightId.Should().Be(night.Id);
        result.HostUserId.Should().Be(night.OrganizerId);
    }

    [Fact]
    public async Task Handle_ALivePageIsCached_UntilItsTagIsDropped_Then_TheLinkSaysTheNightIsCancelled()
    {
        // The contract between the query and whoever cancels a night (#4856): the page is cached under
        // CacheTagPrefix + token; dropping that tag is what turns a live page into the cancelled one. Real
        // HybridCache, so the tag itself is exercised and not a mock of it.
        var invitation = Reconstitute(GameNightInvitationStatus.Accepted);
        var night = NightWithAGame(Guid.NewGuid());
        GivenTheNight(invitation, night);

        var live = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);
        night.Cancel();
        var stillCached = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);
        await _cache.RemoveByTagAsync(
            GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + Token, CancellationToken.None);
        var afterTheDrop = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        live!.NightCancelled.Should().BeFalse();
        live.Title.Should().Be("Friday Night Catan");
        stillCached!.NightCancelled.Should().BeFalse("the page is cached until its tag is dropped");
        afterTheDrop!.NightCancelled.Should().BeTrue();
        afterTheDrop.Title.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AWithdrawnInvitationOnACancelledNight_IsStillJustWithdrawn_AndShowsNothing()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Cancelled);
        var night = NightWithAGame(Guid.NewGuid());
        night.Cancel();
        GivenTheNight(invitation, night);

        var result = await _sut.Handle(new GetGameNightInvitationByTokenQuery(Token), CancellationToken.None);

        result!.Status.Should().Be("Cancelled");
        result.NightCancelled.Should().BeTrue("both are true: the page says the night is off");
        AssertNothingOfTheNightIsShown(result);
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
