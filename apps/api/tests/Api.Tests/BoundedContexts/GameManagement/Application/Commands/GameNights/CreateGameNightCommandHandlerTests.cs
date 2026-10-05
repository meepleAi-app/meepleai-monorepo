using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Unit tests for <see cref="CreateGameNightCommandHandler"/>.
/// Issue #46: original CreateGameNight flow.
/// Issue #950 (W1-PR1): InvitedEmails extension — handler chains to
/// <see cref="CreateGameNightInvitationByEmailCommand"/> per email via
/// <see cref="IMediator"/> after the game night aggregate is persisted.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateGameNightCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _gameNightRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly CreateGameNightCommandHandler _sut;

    public CreateGameNightCommandHandlerTests()
    {
        _sut = new CreateGameNightCommandHandler(
            _gameNightRepoMock.Object,
            _unitOfWorkMock.Object,
            _mediatorMock.Object);
    }

    private static CreateGameNightCommand MakeCommand(
        Guid userId,
        List<Guid>? invitedUserIds = null,
        List<string>? invitedEmails = null)
    {
        return new CreateGameNightCommand(
            UserId: userId,
            Title: "Friday Night Catan",
            ScheduledAt: DateTimeOffset.UtcNow.AddDays(7),
            Description: null,
            Location: "My place",
            MaxPlayers: 5,
            GameIds: null,
            InvitedUserIds: invitedUserIds,
            InvitedEmails: invitedEmails);
    }

    private void SetupInvitationMediatorOk()
    {
        _mediatorMock
            .Setup(m => m.Send(
                It.IsAny<CreateGameNightInvitationByEmailCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameNightInvitationDto(
                Id: Guid.NewGuid(),
                Token: "stub-token",
                GameNightId: Guid.NewGuid(),
                Email: "stub@example.com",
                Status: "Pending",
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(14),
                RespondedAt: null,
                RespondedByUserId: null,
                CreatedAt: DateTimeOffset.UtcNow,
                CreatedBy: Guid.NewGuid()));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Backward compatibility — no emails behaves exactly as before
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoInvitedEmails_DoesNotDispatchInvitationCommand()
    {
        var command = MakeCommand(Guid.NewGuid());

        await _sut.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(
            m => m.Send(
                It.IsAny<CreateGameNightInvitationByEmailCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyInvitedEmails_DoesNotDispatchInvitationCommand()
    {
        var command = MakeCommand(Guid.NewGuid(), invitedEmails: new List<string>());

        await _sut.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(
            m => m.Send(
                It.IsAny<CreateGameNightInvitationByEmailCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNoInvitedEmails_PersistsGameNightOnce()
    {
        var command = MakeCommand(Guid.NewGuid());

        await _sut.Handle(command, CancellationToken.None);

        _gameNightRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // New behavior — chain sub-command per email after game night persists
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithInvitedEmails_DispatchesInvitationCommandPerEmail()
    {
        SetupInvitationMediatorOk();
        var organizerId = Guid.NewGuid();
        var emails = new List<string> { "alice@example.com", "bob@example.com", "carol@example.com" };
        var command = MakeCommand(organizerId, invitedEmails: emails);

        await _sut.Handle(command, CancellationToken.None);

        foreach (var email in emails)
        {
            _mediatorMock.Verify(
                m => m.Send(
                    It.Is<CreateGameNightInvitationByEmailCommand>(c =>
                        c.Email == email && c.OrganizerUserId == organizerId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Handle_WithInvitedEmails_DispatchesAfterGameNightPersisted()
    {
        SetupInvitationMediatorOk();
        var sequence = new MockSequence();
        _gameNightRepoMock.InSequence(sequence)
            .Setup(r => r.AddAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.InSequence(sequence)
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mediatorMock.InSequence(sequence)
            .Setup(m => m.Send(
                It.IsAny<CreateGameNightInvitationByEmailCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameNightInvitationDto(
                Id: Guid.NewGuid(),
                Token: "stub",
                GameNightId: Guid.NewGuid(),
                Email: "alice@example.com",
                Status: "Pending",
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(14),
                RespondedAt: null,
                RespondedByUserId: null,
                CreatedAt: DateTimeOffset.UtcNow,
                CreatedBy: Guid.NewGuid()));

        var command = MakeCommand(
            Guid.NewGuid(),
            invitedEmails: new List<string> { "alice@example.com" });

        await _sut.Handle(command, CancellationToken.None);

        _gameNightRepoMock.VerifyAll();
        _unitOfWorkMock.VerifyAll();
    }

    [Fact]
    public async Task Handle_WithInvitedEmails_PassesCreatedGameNightIdToSubCommand()
    {
        SetupInvitationMediatorOk();
        var command = MakeCommand(
            Guid.NewGuid(),
            invitedEmails: new List<string> { "alice@example.com" });
        Guid capturedGameNightId = Guid.Empty;
        _gameNightRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightEvent, CancellationToken>((gn, _) => capturedGameNightId = gn.Id)
            .Returns(Task.CompletedTask);

        await _sut.Handle(command, CancellationToken.None);

        capturedGameNightId.Should().NotBe(Guid.Empty);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateGameNightInvitationByEmailCommand>(c =>
                    c.GameNightId == capturedGameNightId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMixedInviteesAndEmails_StillPreInvitesUserIdsAndChainsEmails()
    {
        SetupInvitationMediatorOk();
        var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var emails = new List<string> { "alice@example.com", "bob@example.com" };
        var command = MakeCommand(
            Guid.NewGuid(),
            invitedUserIds: userIds,
            invitedEmails: emails);
        GameNightEvent? captured = null;
        _gameNightRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightEvent, CancellationToken>((gn, _) => captured = gn)
            .Returns(Task.CompletedTask);

        await _sut.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Rsvps.Should().HaveCount(2, "PreInvite must register an RSVP for each registered user ID");
        _mediatorMock.Verify(
            m => m.Send(
                It.IsAny<CreateGameNightInvitationByEmailCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ReturnsCreatedGameNightId()
    {
        var command = MakeCommand(Guid.NewGuid());
        Guid expectedId = Guid.Empty;
        _gameNightRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightEvent, CancellationToken>((gn, _) => expectedId = gn.Id)
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().Be(expectedId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // #4055 — l'offset del client non deve arrivare alla colonna timestamptz
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// #4055. Il command porta <c>DateTimeOffset ScheduledAt</c>, quindi l'offset del client
    /// arriva fino a qui; <c>scheduled_at</c> e' <c>timestamptz</c> e Npgsql rifiuta un offset
    /// diverso da zero, con un'eccezione che esplode a <c>SaveChangesAsync</c> — dopo i validator
    /// — e che il chiamante vede come 500 (misurato sullo stack locale il 2026-10-04:
    /// <c>+01:00</c> e <c>-05:00</c> → 500, <c>Z</c> → 201).
    /// </summary>
    /// <remarks>
    /// L'asserzione e' sull'aggregato consegnato al repository, cioe' su cio' che viene scritto:
    /// un test sul solo command non direbbe nulla, e il mock di <c>IUnitOfWork</c> non puo'
    /// riprodurre il rifiuto di Npgsql (quello sta in
    /// <c>Integration/GameManagement/GameNightScheduledAtTimezoneTests</c>).
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-5)]
    public async Task Handle_WithNonUtcScheduledAt_PersistsTheSameInstantInUtc(int offsetHours)
    {
        var scheduledAt = new DateTimeOffset(2026, 12, 20, 20, 0, 0, TimeSpan.FromHours(offsetHours));
        var command = new CreateGameNightCommand(
            UserId: Guid.NewGuid(),
            Title: "Serata con offset",
            ScheduledAt: scheduledAt);
        GameNightEvent? captured = null;
        _gameNightRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightEvent, CancellationToken>((gn, _) => captured = gn)
            .Returns(Task.CompletedTask);

        await _sut.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
        captured.ScheduledAt.Should().Be(scheduledAt, "la normalizzazione non deve spostare l'istante");
    }
}
