using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Unit tests for <see cref="CreateGameNightInvitationByEmailCommandHandler"/>.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// Verifies organizer authorization, duplicate-pending guard, and the email
/// dispatch side-effect (D1 a — see executive spec §5).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateGameNightInvitationByEmailCommandHandlerTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private const string BaseUrl = "https://meepleai.test";

    private readonly Mock<IGameNightInvitationRepository> _invitationRepoMock = new();
    private readonly Mock<IGameNightEventRepository> _gameNightRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IGameRepository> _gameRepoMock = new();
    private readonly Mock<IGameNightEmailService> _emailServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new(UtcNow);
    private readonly CreateGameNightInvitationByEmailCommandHandler _sut;

    public CreateGameNightInvitationByEmailCommandHandlerTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:BaseUrl"] = BaseUrl,
            })
            .Build();

        _sut = new CreateGameNightInvitationByEmailCommandHandler(
            _invitationRepoMock.Object,
            _gameNightRepoMock.Object,
            _userRepoMock.Object,
            _gameRepoMock.Object,
            _emailServiceMock.Object,
            _unitOfWorkMock.Object,
            configuration,
            _timeProvider);
    }

    private static GameNightEvent CreateGameNight(Guid organizerId)
    {
        return GameNightEvent.Create(
            organizerId: organizerId,
            title: "Friday Night Catan",
            scheduledAt: UtcNow.AddDays(7),
            description: null,
            location: "My place",
            maxPlayers: 5,
            gameIds: null);
    }

    [Fact]
    public async Task Handle_GameNightNotFound_ThrowsNotFoundException()
    {
        var gameNightId = Guid.NewGuid();
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(gameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var command = new CreateGameNightInvitationByEmailCommand(
            gameNightId, "guest@example.com", Guid.NewGuid());

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CallerIsNotOrganizer_ThrowsUnauthorizedAccessException()
    {
        var organizerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var gameNight = CreateGameNight(organizerId);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var command = new CreateGameNightInvitationByEmailCommand(
            gameNight.Id, "guest@example.com", attackerId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _invitationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicatePendingInvitation_ThrowsConflictException()
    {
        var organizerId = Guid.NewGuid();
        var gameNight = CreateGameNight(organizerId);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _invitationRepoMock
            .Setup(r => r.ExistsPendingByEmailAsync(
                gameNight.Id, "guest@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateGameNightInvitationByEmailCommand(
            gameNight.Id, "  GUEST@example.com  ", organizerId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        // Duplicate guard normalizes email (trim + lowercase) before lookup.
        _invitationRepoMock.Verify(
            r => r.ExistsPendingByEmailAsync(
                gameNight.Id, "guest@example.com", It.IsAny<CancellationToken>()),
            Times.Once);
        _invitationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_HappyPath_PersistsAggregateAndDispatchesEmail()
    {
        var organizerId = Guid.NewGuid();
        var gameNight = CreateGameNight(organizerId);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _invitationRepoMock
            .Setup(r => r.ExistsPendingByEmailAsync(
                gameNight.Id, "guest@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.Authentication.Domain.Entities.User?)null);

        GameNightInvitation? captured = null;
        _invitationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightInvitation, CancellationToken>((inv, _) => captured = inv);

        var command = new CreateGameNightInvitationByEmailCommand(
            gameNight.Id, "Guest@example.com", organizerId);

        var dto = await _sut.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(GameNightInvitationStatus.Pending);
        captured.Email.Should().Be("guest@example.com");
        captured.ExpiresAt.Should().Be(UtcNow.AddDays(14)); // DefaultExpiryDays.

        _invitationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Email dispatched after persistence with token-bearing accept/decline URLs.
        _emailServiceMock.Verify(
            e => e.SendGameNightInvitationEmailAsync(
                "guest@example.com",
                "A friend", // Falls back to "A friend" when organizer is null.
                gameNight.Title,
                gameNight.ScheduledAt,
                gameNight.Location,
                It.IsAny<IReadOnlyList<string>>(),
                $"{BaseUrl}/invites/{captured.Token}?action=accept",
                $"{BaseUrl}/invites/{captured.Token}?action=decline",
                $"{BaseUrl}/invites/{captured.Token}/unsubscribe",
                It.IsAny<CancellationToken>()),
            Times.Once);

        dto.Token.Should().Be(captured.Token);
        dto.Status.Should().Be(nameof(GameNightInvitationStatus.Pending));
        dto.GameNightId.Should().Be(gameNight.Id);
    }

    [Fact]
    public async Task Handle_OverridesDefaultExpiryFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:BaseUrl"] = BaseUrl,
                ["GameNight:InvitationExpiryDays"] = "30",
            })
            .Build();

        var sut = new CreateGameNightInvitationByEmailCommandHandler(
            _invitationRepoMock.Object,
            _gameNightRepoMock.Object,
            _userRepoMock.Object,
            _gameRepoMock.Object,
            _emailServiceMock.Object,
            _unitOfWorkMock.Object,
            configuration,
            _timeProvider);

        var organizerId = Guid.NewGuid();
        var gameNight = CreateGameNight(organizerId);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _invitationRepoMock
            .Setup(r => r.ExistsPendingByEmailAsync(
                gameNight.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        GameNightInvitation? captured = null;
        _invitationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightInvitation, CancellationToken>((inv, _) => captured = inv);

        var command = new CreateGameNightInvitationByEmailCommand(
            gameNight.Id, "guest@example.com", organizerId);

        await sut.Handle(command, CancellationToken.None);

        captured!.ExpiresAt.Should().Be(UtcNow.AddDays(30));
    }
}
